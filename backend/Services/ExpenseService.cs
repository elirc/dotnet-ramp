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
        var expense = new Expense
        {
            EmployeeId = actor.Id,
            Merchant = req.Merchant.Trim(),
            Description = req.Description.Trim(),
            Amount = req.Amount,
            Currency = req.Currency.ToUpperInvariant(),
            Category = req.Category,
            ExpenseDate = req.ExpenseDate,
            Status = ExpenseStatus.Draft
        };

        RefreshPolicy(expense);
        _store.Expenses[expense.Id] = expense;
        return expense;
    }

    public Expense Update(User actor, Guid id, UpdateExpenseRequest req)
    {
        var expense = GetOwned(actor, id);
        if (!expense.IsEditable)
            throw new DomainException($"An expense in '{expense.Status}' status cannot be edited.");

        expense.Merchant = req.Merchant.Trim();
        expense.Description = req.Description.Trim();
        expense.Amount = req.Amount;
        expense.Currency = req.Currency.ToUpperInvariant();
        expense.Category = req.Category;
        expense.ExpenseDate = req.ExpenseDate;
        Touch(expense);
        RefreshPolicy(expense);
        return expense;
    }

    public void Delete(User actor, Guid id)
    {
        var expense = GetOwned(actor, id);
        if (expense.Status != ExpenseStatus.Draft)
            throw new DomainException("Only draft expenses can be deleted.");

        _store.Expenses.TryRemove(id, out _);
        if (expense.ReceiptId is { } rid)
            _store.Receipts.TryRemove(rid, out _);
    }

    public Expense Submit(User actor, Guid id)
    {
        var expense = GetOwned(actor, id);
        RefreshPolicy(expense);

        if (PolicyEngine.HasBlocking(expense.PolicyFlags))
        {
            var reasons = expense.PolicyFlags
                .Where(f => f.Severity == PolicySeverity.Blocking)
                .Select(f => f.Message)
                .ToList();
            throw new DomainException("Expense violates policy and cannot be submitted.", reasons);
        }

        Transition(expense, ExpenseStatus.Submitted, actor, note: null);
        return expense;
    }

    public Expense Approve(User actor, Guid id)
    {
        var expense = GetForActor(actor, id);
        EnsureManagesOwner(actor, expense);
        Transition(expense, ExpenseStatus.Approved, actor, note: null);
        return expense;
    }

    public Expense Reject(User actor, Guid id, string reason)
    {
        var expense = GetForActor(actor, id);
        EnsureManagesOwner(actor, expense);
        expense.RejectionReason = reason.Trim();
        Transition(expense, ExpenseStatus.Rejected, actor, note: reason.Trim());
        return expense;
    }

    /// <summary>Finance starts reimbursement; the actual payment completes in the background.</summary>
    public Expense StartReimbursement(User actor, Guid id)
    {
        var expense = GetForActor(actor, id);
        Transition(expense, ExpenseStatus.Reimbursing, actor, note: "Reimbursement queued.");
        _queue.EnqueueAsync(BackgroundJobs.ProcessReimbursement(expense.Id));
        return expense;
    }

    /// <summary>Re-runs policy after a receipt finishes processing (called by the receipt flow).</summary>
    public void RefreshPolicy(Expense expense)
    {
        bool hasProcessedReceipt = expense.ReceiptId is { } rid
            && _store.Receipts.GetValueOrDefault(rid)?.Status == ReceiptStatus.Processed;
        expense.PolicyFlags = _policy.Evaluate(expense, hasProcessedReceipt).ToList();
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
