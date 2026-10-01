using ExpenseManagement.Api.Background;
using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Services;
using ExpenseManagement.Api.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ExpenseManagement.Api.Tests;

public sealed class ExpenseWorkflowTests
{
    [Fact]
    public void Reject_on_draft_does_not_change_reason_history_or_timestamp()
    {
        var (store, employee, manager, _) = Fixture();
        var expense = AddExpense(store, employee, ExpenseStatus.Draft);
        expense.RejectionReason = null;
        var historyCount = expense.History.Count;
        var updatedAt = expense.UpdatedAt;
        var service = Service(store, new RecordingQueue());

        Assert.Throws<DomainException>(() => service.Reject(manager, expense.Id, "Needs an itemized receipt"));

        Assert.Equal(ExpenseStatus.Draft, expense.Status);
        Assert.Null(expense.RejectionReason);
        Assert.Equal(historyCount, expense.History.Count);
        Assert.Equal(updatedAt, expense.UpdatedAt);
    }

    [Fact]
    public void Invalid_update_leaves_live_entity_unchanged()
    {
        var (store, employee, _, _) = Fixture();
        var expense = AddExpense(store, employee, ExpenseStatus.Draft);
        var before = Snapshot(expense);
        var service = Service(store, new RecordingQueue());

        Assert.Throws<DomainException>(() => service.Update(employee, expense.Id, new UpdateExpenseRequest
        {
            Merchant = "  changed ", Description = "changed", Amount = 12m,
            Currency = "US", Category = ExpenseCategory.Meals, ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        }));

        Assert.Equal(before, Snapshot(expense));
    }

    [Fact]
    public void Create_and_update_normalize_values_before_persisting()
    {
        var (store, employee, _, _) = Fixture();
        var service = Service(store, new RecordingQueue());

        var expense = service.Create(employee, new CreateExpenseRequest
        {
            Merchant = "  Acme  ", Description = "  Office supplies  ", Amount = 12m,
            Currency = " usd ", Category = ExpenseCategory.OfficeSupplies,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        Assert.Equal("Acme", expense.Merchant);
        Assert.Equal("Office supplies", expense.Description);
        Assert.Equal("USD", expense.Currency);

        service.Update(employee, expense.Id, new UpdateExpenseRequest
        {
            Merchant = "  Northwind  ", Description = "  Travel snacks  ", Amount = 8m,
            Currency = " eur ", Category = ExpenseCategory.Meals,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        Assert.Equal("Northwind", expense.Merchant);
        Assert.Equal("Travel snacks", expense.Description);
        Assert.Equal("EUR", expense.Currency);
    }

    [Fact]
    public void Create_rejects_each_invalid_field_without_inserting()
    {
        var (store, employee, _, _) = Fixture();
        var service = Service(store, new RecordingQueue());
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cases = new[]
        {
            new CreateExpenseRequest { Merchant = " ", Description = "Valid", Amount = 1m, Currency = "USD", Category = ExpenseCategory.Meals, ExpenseDate = today },
            new CreateExpenseRequest { Merchant = "Valid", Description = " ", Amount = 1m, Currency = "USD", Category = ExpenseCategory.Meals, ExpenseDate = today },
            new CreateExpenseRequest { Merchant = "Valid", Description = "Valid", Amount = 0m, Currency = "USD", Category = ExpenseCategory.Meals, ExpenseDate = today },
            new CreateExpenseRequest { Merchant = "Valid", Description = "Valid", Amount = 1m, Currency = "USD", Category = (ExpenseCategory)999, ExpenseDate = today },
            new CreateExpenseRequest { Merchant = "Valid", Description = "Valid", Amount = 1m, Currency = "USD", Category = ExpenseCategory.Meals, ExpenseDate = today.AddDays(1) }
        };

        foreach (var request in cases)
        {
            var count = store.Expenses.Count;
            Assert.Throws<DomainException>(() => service.Create(employee, request));
            Assert.Equal(count, store.Expenses.Count);
        }
    }

    [Fact]
    public async Task Bounded_queue_admits_capacity_then_rejects_until_dequeued()
    {
        var queue = new BackgroundTaskQueue(capacity: 1);
        Func<IServiceProvider, CancellationToken, Task> work = (_, _) => Task.CompletedTask;

        Assert.True(queue.TryEnqueue(work));
        Assert.False(queue.TryEnqueue(work));
        var dequeued = await queue.DequeueAsync(CancellationToken.None);
        Assert.Same(work, dequeued);
        Assert.True(queue.TryEnqueue(work));
    }

    [Fact]
    public async Task Discarded_WriteAsync_on_a_full_queue_parks_and_lands_after_a_dequeue()
    {
        // BackgroundTaskQueue is built with FullMode = BoundedChannelFullMode.Wait.
        // A discarded WriteAsync on a full channel does NOT drop the item: it parks a
        // pending writer that completes when capacity frees. That is why the original
        // StartReimbursement bug was "the exception and the back-pressure were lost",
        // not "the job was silently dropped".
        var queue = new BackgroundTaskQueue(capacity: 1);
        Func<IServiceProvider, CancellationToken, Task> first = (_, _) => Task.CompletedTask;
        Func<IServiceProvider, CancellationToken, Task> parked = (_, _) => Task.CompletedTask;

        Assert.True(queue.TryEnqueue(first));

        // Fire-and-forget exactly as the old code did.
        var discarded = queue.EnqueueAsync(parked).AsTask();

        // The queue really is full: a non-blocking attempt is refused...
        Assert.False(queue.TryEnqueue((_, _) => Task.CompletedTask));
        // ...and the parked writer has not completed.
        Assert.False(discarded.IsCompleted);

        Assert.Same(first, await queue.DequeueAsync(CancellationToken.None));

        // Capacity freed: the writer nobody awaited lands anyway.
        await discarded.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Same(parked, await queue.DequeueAsync(CancellationToken.None));
    }

    [Fact]
    public void Blocked_submit_does_not_refresh_or_mutate_policy_state()
    {
        var (store, employee, _, _) = Fixture();
        var expense = AddExpense(store, employee, ExpenseStatus.Draft, amount: 50m);
        var beforeFlags = expense.PolicyFlags.ToArray();
        var beforeHistory = expense.History.ToArray();
        var beforeUpdatedAt = expense.UpdatedAt;
        var service = Service(store, new RecordingQueue());

        Assert.Throws<DomainException>(() => service.Submit(employee, expense.Id));

        Assert.Equal(ExpenseStatus.Draft, expense.Status);
        Assert.Equal(beforeFlags, expense.PolicyFlags);
        Assert.Equal(beforeHistory, expense.History);
        Assert.Equal(beforeUpdatedAt, expense.UpdatedAt);
    }

    [Fact]
    public void Full_reimbursement_queue_leaves_approved_expense_unchanged()
    {
        var (store, employee, _, finance) = Fixture();
        var expense = AddExpense(store, employee, ExpenseStatus.Approved);
        var before = Snapshot(expense);
        var service = Service(store, new RecordingQueue { Accept = false });

        Assert.Throws<DomainException>(() => service.StartReimbursement(finance, expense.Id));

        Assert.Equal(before, Snapshot(expense));
    }

    [Fact]
    public void Throwing_reimbursement_queue_leaves_approved_expense_unchanged()
    {
        var (store, employee, _, finance) = Fixture();
        var expense = AddExpense(store, employee, ExpenseStatus.Approved);
        var before = Snapshot(expense);
        var service = Service(store, new RecordingQueue { Accept = true, ThrowOnTryWrite = true });

        Assert.Throws<InvalidOperationException>(() => service.StartReimbursement(finance, expense.Id));

        Assert.Equal(before, Snapshot(expense));
    }

    [Fact]
    public async Task Accepted_reimbursement_records_one_transition_and_duplicate_jobs_complete_once()
    {
        var (store, employee, _, finance) = Fixture();
        var queue = new RecordingQueue { Accept = true };
        var expense = AddExpense(store, employee, ExpenseStatus.Approved);
        var service = Service(store, queue);

        service.StartReimbursement(finance, expense.Id);

        Assert.Equal(ExpenseStatus.Reimbursing, expense.Status);
        Assert.Single(expense.History);
        Assert.Equal(ExpenseStatus.Reimbursing, expense.History[0].To);
        Assert.Single(queue.Work);
        Assert.Throws<DomainException>(() => service.StartReimbursement(finance, expense.Id));
        Assert.Single(queue.Work);

        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(store)
            .BuildServiceProvider();
        var job = BackgroundJobs.ProcessReimbursement(expense.Id, TimeSpan.Zero);
        await Task.WhenAll(
            job(services, CancellationToken.None),
            job(services, CancellationToken.None));

        Assert.Equal(ExpenseStatus.Reimbursed, expense.Status);
        Assert.StartsWith("PMT-", expense.PaymentReference);
        Assert.Equal(2, expense.History.Count);
        Assert.Equal(1, expense.History.Count(e => e.To == ExpenseStatus.Reimbursed));
    }

    [Fact]
    public void System_transition_only_allows_marked_worker_edge()
    {
        Assert.True(ExpenseStateMachine.CanSystemTransition(ExpenseStatus.Reimbursing, ExpenseStatus.Reimbursed));
        Assert.False(ExpenseStateMachine.CanSystemTransition(ExpenseStatus.Submitted, ExpenseStatus.Approved));
        Assert.False(ExpenseStateMachine.CanSystemTransition(ExpenseStatus.Approved, ExpenseStatus.Reimbursing));
    }

    private static ExpenseService Service(DataStore store, IBackgroundTaskQueue queue) =>
        new(store, new PolicyEngine(), queue);

    private static (DataStore Store, User Employee, User Manager, User Finance) Fixture()
    {
        var store = new DataStore();
        var manager = new User { Email = "manager@test", FullName = "Test Manager", PasswordHash = "x", PasswordSalt = "x", Role = UserRole.Manager };
        var employee = new User { Email = "employee@test", FullName = "Test Employee", PasswordHash = "x", PasswordSalt = "x", Role = UserRole.Employee, ManagerId = manager.Id };
        var finance = new User { Email = "finance@test", FullName = "Test Finance", PasswordHash = "x", PasswordSalt = "x", Role = UserRole.Finance };
        store.Users[manager.Id] = manager;
        store.Users[employee.Id] = employee;
        store.Users[finance.Id] = finance;
        return (store, employee, manager, finance);
    }

    private static Expense AddExpense(DataStore store, User employee, ExpenseStatus status, decimal amount = 10m)
    {
        var expense = new Expense
        {
            EmployeeId = employee.Id,
            Merchant = "Cafe",
            Description = "Team lunch",
            Amount = amount,
            Currency = "USD",
            Category = ExpenseCategory.Meals,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = status
        };
        store.Expenses[expense.Id] = expense;
        return expense;
    }

    private static string Snapshot(Expense e) =>
        $"{e.Status}|{e.Merchant}|{e.Description}|{e.Amount}|{e.Currency}|{e.Category}|{e.ExpenseDate}|{e.RejectionReason}|{e.PaymentReference}|{e.UpdatedAt:o}|{e.PolicyFlags.Count}|{e.History.Count}";

    private sealed class RecordingQueue : IBackgroundTaskQueue
    {
        public bool Accept { get; init; }
        public bool ThrowOnTryWrite { get; init; }
        public List<Func<IServiceProvider, CancellationToken, Task>> Work { get; } = new();

        public bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work)
        {
            if (ThrowOnTryWrite) throw new InvalidOperationException("queue unavailable");
            if (!Accept) return false;
            Work.Add(work);
            return true;
        }

        public ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work)
        {
            Work.Add(work);
            return ValueTask.CompletedTask;
        }

        public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
