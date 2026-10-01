using ExpenseManagement.Api.Background;
using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Storage;

namespace ExpenseManagement.Api.Services;

public record ExpenseFilter(ExpenseStatus? Status, ExpenseCategory? Category, DateOnly? From, DateOnly? To);

/// <summary>
/// Core expense workflow: CRUD, policy-gated submission, role-gated approvals,
/// and reimbursement hand-off to the background queue. All authorization
/// (who can see/act on what) is enforced here, not in the controllers.
/// </summary>
public class ExpenseService
{
    private readonly DataStore _store;
    private readonly PolicyEngine _policy;
    private readonly IBackgroundTaskQueue _queue;

    public ExpenseService(DataStore store, PolicyEngine policy, IBackgroundTaskQueue queue)
    {
        _store = store;
        _policy = policy;
        _queue = queue;
    }

    // ---- Queries (role-scoped) ----

    public IReadOnlyList<Expense> List(User actor, ExpenseFilter filter)
    {
        IEnumerable<Expense> query = _store.Expenses.Values;

        query = actor.Role switch
        {
            // Employees see only their own expenses.
            UserRole.Employee => query.Where(e => e.EmployeeId == actor.Id),
            // Managers see their own plus their direct reports'.
            UserRole.Manager => query.Where(e =>
                e.EmployeeId == actor.Id || IsDirectReport(e.EmployeeId, actor.Id)),
            // Finance sees everything.
            UserRole.Finance => query,
            _ => Enumerable.Empty<Expense>()
        };

        if (filter.Status is { } s) query = query.Where(e => e.Status == s);
        if (filter.Category is { } c) query = query.Where(e => e.Category == c);
        if (filter.From is { } from) query = query.Where(e => e.ExpenseDate >= from);
        if (filter.To is { } to) query = query.Where(e => e.ExpenseDate <= to);

        return query.OrderByDescending(e => e.UpdatedAt).ToList();
    }

    public Expense GetForActor(User actor, Guid id)
    {
        var expense = _store.Expenses.GetValueOrDefault(id)
            ?? throw new NotFoundException("Expense not found.");

        if (!CanView(actor, expense))
            throw new ForbiddenException("You do not have access to this expense.");

        return expense;
    }

    // ---- Commands ----

    public Expense Create(User actor, CreateExpenseRequest req)
    {
        var values = ValidateAndNormalize(req.Merchant, req.Description, req.Amount,
            req.Currency, req.Category, req.ExpenseDate);
        var expense = new Expense
        {
            EmployeeId = actor.Id,
            Merchant = values.Merchant,
            Description = values.Description,
            Amount = values.Amount,
            Currency = values.Currency,
            Category = values.Category,
            ExpenseDate = values.ExpenseDate,
            Status = ExpenseStatus.Draft
        };

        RefreshPolicy(expense);
        _store.Expenses[expense.Id] = expense;
        return expense;
    }

    public Expense Update(User actor, Guid id, UpdateExpenseRequest req)
    {
        var values = ValidateAndNormalize(req.Merchant, req.Description, req.Amount,
            req.Currency, req.Category, req.ExpenseDate);
        lock (_store.GetExpenseLock(id))
        {
            var expense = GetOwned(actor, id);
            if (!expense.IsEditable)
                throw new DomainException($"An expense in '{expense.Status}' status cannot be edited.");

            var candidate = new Expense
            {
                Id = expense.Id,
                EmployeeId = expense.EmployeeId,
                Merchant = values.Merchant,
                Description = values.Description,
                Amount = values.Amount,
                Currency = values.Currency,
                Category = values.Category,
                ExpenseDate = values.ExpenseDate,
                Status = expense.Status,
                ReceiptId = expense.ReceiptId
            };
            var flags = EvaluatePolicy(candidate).ToList();

            expense.Merchant = values.Merchant;
            expense.Description = values.Description;
            expense.Amount = values.Amount;
            expense.Currency = values.Currency;
            expense.Category = values.Category;
            expense.ExpenseDate = values.ExpenseDate;
            expense.PolicyFlags = flags;
            Touch(expense);
            return expense;
        }
    }

    public void Delete(User actor, Guid id)
    {
        lock (_store.GetExpenseLock(id))
        {
            var expense = GetOwned(actor, id);
            if (expense.Status != ExpenseStatus.Draft)
                throw new DomainException("Only draft expenses can be deleted.");

            _store.Expenses.TryRemove(id, out _);
            if (expense.ReceiptId is { } rid)
                _store.Receipts.TryRemove(rid, out _);
        }
    }

    public Expense Submit(User actor, Guid id)
    {
        lock (_store.GetExpenseLock(id))
        {
            var expense = GetOwned(actor, id);
            EnsureLegalHumanTransition(expense, ExpenseStatus.Submitted, actor);
            var flags = EvaluatePolicy(expense).ToList();
            if (PolicyEngine.HasBlocking(flags))
            {
                var reasons = flags
                .Where(f => f.Severity == PolicySeverity.Blocking)
                .Select(f => f.Message)
                .ToList();
                throw new DomainException("Expense violates policy and cannot be submitted.", reasons);
            }

            expense.PolicyFlags = flags;
            Transition(expense, ExpenseStatus.Submitted, actor, note: null);
            return expense;
        }
    }

    public Expense Approve(User actor, Guid id)
    {
        lock (_store.GetExpenseLock(id))
        {
            var expense = GetForActor(actor, id);
            EnsureManagesOwner(actor, expense);
            Transition(expense, ExpenseStatus.Approved, actor, note: null);
            return expense;
        }
    }

    public Expense Reject(User actor, Guid id, string reason)
    {
        var normalizedReason = ValidateReason(reason);
        lock (_store.GetExpenseLock(id))
        {
            var expense = GetForActor(actor, id);
            EnsureManagesOwner(actor, expense);
            EnsureLegalHumanTransition(expense, ExpenseStatus.Rejected, actor);
            expense.RejectionReason = normalizedReason;
            Transition(expense, ExpenseStatus.Rejected, actor, note: normalizedReason);
            return expense;
        }
    }

    /// <summary>Finance starts reimbursement; the actual payment completes in the background.</summary>
    public Expense StartReimbursement(User actor, Guid id)
    {
        lock (_store.GetExpenseLock(id))
        {
            var expense = GetForActor(actor, id);
            EnsureLegalHumanTransition(expense, ExpenseStatus.Reimbursing, actor);
            // TryWrite is the admission boundary. The worker takes the same gate,
            // so it cannot observe the expense until this transition is complete.
            if (!_queue.TryEnqueue(BackgroundJobs.ProcessReimbursement(expense.Id)))
                throw new DomainException("Reimbursement queue is full; try again later.");

            Transition(expense, ExpenseStatus.Reimbursing, actor, note: "Reimbursement queued.");
            return expense;
        }
    }

    /// <summary>Re-runs policy after a receipt finishes processing (called by the receipt flow).</summary>
    public void RefreshPolicy(Expense expense)
    {
        expense.PolicyFlags = EvaluatePolicy(expense).ToList();
    }

    private IReadOnlyList<PolicyFlag> EvaluatePolicy(Expense expense)
    {
        bool hasProcessedReceipt = expense.ReceiptId is { } rid
            && _store.Receipts.GetValueOrDefault(rid)?.Status == ReceiptStatus.Processed;
        return _policy.Evaluate(expense, hasProcessedReceipt);
    }

    // ---- Internals ----

    /// <summary>The one place a status actually changes. Validates the edge + records history.</summary>
    private void Transition(Expense expense, ExpenseStatus to, User actor, string? note)
    {
        if (!ExpenseStateMachine.CanTransition(expense.Status, to, actor.Role))
            throw new DomainException(
                $"Cannot move expense from '{expense.Status}' to '{to}' as {actor.Role}.");

        var from = expense.Status;
        expense.Status = to;
        Touch(expense);
        expense.History.Add(new ExpenseEvent(from, to, actor.Id, actor.FullName, note, DateTimeOffset.UtcNow));
    }

    private static void EnsureLegalHumanTransition(Expense expense, ExpenseStatus to, User actor)
    {
        if (!ExpenseStateMachine.CanTransition(expense.Status, to, actor.Role))
            throw new DomainException(
                $"Cannot move expense from '{expense.Status}' to '{to}' as {actor.Role}.");
    }

    private static (string Merchant, string Description, decimal Amount, string Currency,
        ExpenseCategory Category, DateOnly ExpenseDate) ValidateAndNormalize(
        string? merchant, string? description, decimal amount, string? currency,
        ExpenseCategory category, DateOnly expenseDate)
    {
        var normalizedMerchant = merchant?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;
        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        var errors = new List<string>();
        if (normalizedMerchant.Length is 0 or > 200) errors.Add("Merchant is required and must be at most 200 characters.");
        if (normalizedDescription.Length is 0 or > 1000) errors.Add("Description is required and must be at most 1000 characters.");
        if (amount is < 0.01m or > 1_000_000m) errors.Add("Amount must be between 0.01 and 1,000,000.");
        if (normalizedCurrency.Length != 3 || normalizedCurrency.Any(c => c is < 'A' or > 'Z'))
            errors.Add("Currency must be a three-letter code.");
        if (!Enum.IsDefined(category)) errors.Add("Category is invalid.");
        if (expenseDate > DateOnly.FromDateTime(DateTime.UtcNow)) errors.Add("Expense date cannot be in the future.");
        if (errors.Count > 0) throw new DomainException("Expense data is invalid.", errors);
        return (normalizedMerchant, normalizedDescription, amount, normalizedCurrency, category, expenseDate);
    }

    private static string ValidateReason(string? reason)
    {
        var normalized = reason?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > 500)
            throw new DomainException("Rejection reason is required and must be at most 500 characters.");
        return normalized;
    }

    private Expense GetOwned(User actor, Guid id)
    {
        var expense = _store.Expenses.GetValueOrDefault(id)
            ?? throw new NotFoundException("Expense not found.");
        if (expense.EmployeeId != actor.Id)
            throw new ForbiddenException("You can only modify your own expenses.");
        return expense;
    }

    private bool CanView(User actor, Expense expense) => actor.Role switch
    {
        UserRole.Employee => expense.EmployeeId == actor.Id,
        UserRole.Manager => expense.EmployeeId == actor.Id || IsDirectReport(expense.EmployeeId, actor.Id),
        UserRole.Finance => true,
        _ => false
    };

    private void EnsureManagesOwner(User actor, Expense expense)
    {
        if (actor.Role != UserRole.Manager || !IsDirectReport(expense.EmployeeId, actor.Id))
            throw new ForbiddenException("You can only approve expenses for your direct reports.");
    }

    private bool IsDirectReport(Guid employeeId, Guid managerId) =>
        _store.Users.GetValueOrDefault(employeeId)?.ManagerId == managerId;

    private static void Touch(Expense e) => e.UpdatedAt = DateTimeOffset.UtcNow;
}
