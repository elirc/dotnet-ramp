# 06: Solutions and review rubric

Exercise 1 calls the public Create method, which reaches the private normalization path shared with Update. The existing invalid-field matrix already supplies the whitespace and undefined-enum examples. The service should trim first, collect all errors, and throw before inserting or editing. The meaningful assertions are `Assert.Throws<DomainException>`, unchanged `store.Expenses.Count`, and `Assert.DoesNotContain` for the candidate id when applicable. A controller-only solution is incomplete because a background or test caller can bypass MVC validation.

Exercise 2’s solution validates the reason before any aggregate mutation and then proves the manager’s ownership and legal edge inside the gate. The reason belongs in both `RejectionReason` and the event note only after all checks pass. A review should reject code that writes `RejectionReason` before `CanTransition`, even if the controller currently requires Manager.

Exercise 3 is solved with a queue double implementing the production interface. The full branch must throw and leave `Approved`, zero events, and zero captured work. The accepted branch must enqueue exactly once before calling `Transition`. If a reviewer sees transition first followed by `TryEnqueue`, they should ask how a false return is rolled back and whether another reader can observe the temporary state. The gate and nonblocking admission are the key design points.

Exercise 4’s solution uses the production delegate with `TimeSpan.Zero`, a `ServiceCollection`, `.AddLogging()`, and the same `DataStore`. The worker’s check must be inside `lock (store.GetExpenseLock(expenseId))`, alongside payment reference, status, timestamp, and history writes. A test that calls a copied `Complete` helper or only checks `CanSystemTransition` misses the handler race.

Use this review rubric. Correctness: every rejected command leaves all touched fields unchanged. Authorization: service methods enforce ownership and role regardless of HTTP annotations. Concurrency: command and worker share one per-expense gate and the duplicate job is idempotent. Evidence: tests assert positive counts and the TRX counters show execution. Scope: the queue stays process local, receipt handling remains unchanged except for the shared interface, and no fake database or payment rail is introduced.

One deliberate limitation remains: readers return mutable entities from the in-memory store, so this is not a durable transaction or snapshot isolation. A production database version would need an atomic compare-and-set or concurrency token, an outbox, and durable job state. Document that as a follow-up rather than claiming the lock solves cross-process coordination.

## Exercise 2: complete additional regression

Add this method inside ExpenseWorkflowTests, where the existing Fixture, AddExpense and Service helpers are in scope. It is a proposed exercise answer, not an eleventh test in the recorded ten-case run.

```csharp
[Fact]
public void Overlong_rejection_reason_leaves_submitted_expense_unchanged()
{
    var (store, employee, manager, _) = Fixture();
    var expense = AddExpense(store, employee, ExpenseStatus.Submitted);
    var before = Snapshot(expense);
    var service = Service(store, new RecordingQueue());
    Assert.Throws<DomainException>(() =>
        service.Reject(manager, expense.Id, new string('x', 501)));
    Assert.Equal(before, Snapshot(expense));
}
```

## Exercise 3: admission sequence answer

RecordingQueue.Accept is init-only, so use two queue instances. First construct a rejecting queue and service over the fixture store. After its exception, assert Approved, empty history and empty Work. Construct an accepting queue and service over that same store; the next call succeeds with one Reimbursing event and one captured delegate. Call the accepting service once more: the state precondition throws before TryEnqueue, so Work remains at one. The complete recorded fixture below contains the rejection, acceptance and repeated-command assertions in separate tests. Combining them is an exercise, not additional executed evidence.

## Exercise 4: non-reimbursing worker answer

Use Fixture and AddExpense with Submitted. Build a ServiceCollection with AddLogging and AddSingleton(store), capture Snapshot(expense), await ProcessReimbursement(expense.Id, TimeSpan.Zero)(provider, CancellationToken.None), and compare the snapshot. Repeat in a fresh fixture with Reimbursing and assert its single terminal event. The recorded suite checks the state-machine restrictions and duplicate successful worker execution; it does not include this separate Submitted-delegate scenario.

## Exercise 5: asynchronous admission answer

An await cannot remain inside the current C# lock statement. An asynchronous admission design would need an async-compatible coordination mechanism or an explicit reservation/compensation state; the controller would await a Task-returning service. Cancellation before admission should leave Approved and no job. Cancellation after admission requires a defined contract, because cancellation does not remove an already queued delegate. A full queue is a capacity decision, while a canceled request is an interruption. Test each before proposing a durable outbox that records the state change and work request together.

## Complete executed fixture

This is the ExpenseWorkflowTests.cs as delivered, including imports, users, real store, queue double and ten of the eleven facts now in the file (the later `Discarded_WriteAsync_on_a_full_queue_parks_and_lands_after_a_dequeue` test is quoted in chapter 02 instead). Modify that existing file for the exercises. The direct service tests do not exercise HTTP authorization, MVC binding or the hosted service loop. The snapshot string checks field values and list counts; the submit test additionally copies immutable flag/history entries, so it can detect replacement of list contents as well.

```csharp
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
```
