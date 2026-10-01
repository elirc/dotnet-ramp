# Writing Tests Here

Seven recipes. The code blocks are **`// Illustrative — not from this repo`** (no test
project exists yet); they're written to compile against the real types once you scaffold one.

## Scaffold (one time)

```bash
# from repo root
dotnet new xunit -o backend.Tests
dotnet add backend.Tests reference backend/ExpenseManagement.Api.csproj
dotnet test
```

A tiny builder keeps tests readable:

```csharp
// Illustrative — not from this repo
static (DataStore store, User mgr, User emp) SeedTeam() {
    var store = new DataStore();
    var mgr = new User { Email="m@x", FullName="Mgr", Role=UserRole.Manager, PasswordHash="x", PasswordSalt="x" };
    var emp = new User { Email="e@x", FullName="Emp", Role=UserRole.Employee, ManagerId=mgr.Id, PasswordHash="x", PasswordSalt="x" };
    store.Users[mgr.Id]=mgr; store.Users[emp.Id]=emp;
    return (store, mgr, emp);
}
static ExpenseService Svc(DataStore s) => new(s, new PolicyEngine(), new FakeQueue());
```

`FakeQueue` is a no-op `IBackgroundTaskQueue` so enqueue calls don't need a worker.

---

## Recipe 1 — Happy path: state machine legal edges
```csharp
// Illustrative — not from this repo
[Theory]
[InlineData(ExpenseStatus.Draft, ExpenseStatus.Submitted, UserRole.Employee, true)]
[InlineData(ExpenseStatus.Submitted, ExpenseStatus.Approved, UserRole.Manager, true)]
[InlineData(ExpenseStatus.Draft, ExpenseStatus.Approved, UserRole.Manager, false)]
public void Transitions(ExpenseStatus from, ExpenseStatus to, UserRole role, bool ok)
    => Assert.Equal(ok, ExpenseStateMachine.CanTransition(from, to, role));
```
Targets [`ExpenseStateMachine`](../../../backend/Domain/ExpenseStateMachine.cs).

## Recipe 2 — Validation failure: submit blocked by policy
```csharp
// Illustrative — not from this repo
var (store, _, emp) = SeedTeam();
var svc = Svc(store);
var e = svc.Create(emp, new CreateExpenseRequest { Merchant="m", Description="d", Amount=30, Currency="USD", Category=ExpenseCategory.Meals, ExpenseDate=Today() });
var ex = Assert.Throws<DomainException>(() => svc.Submit(emp, e.Id)); // $30, no receipt
Assert.Contains(ex.Details, d => d.Contains("receipt", StringComparison.OrdinalIgnoreCase));
```
Targets [`Submit` L119-126](../../../backend/Services/ExpenseService.cs#L119-L126).

## Recipe 3 — Permission failure: employee can't see others' expenses
```csharp
// Illustrative — not from this repo
Assert.Throws<ForbiddenException>(() => svc.GetForActor(otherEmp, e.Id));
```
Targets [`GetForActor`/`CanView`](../../../backend/Services/ExpenseService.cs#L54-L63).

## Recipe 4 — Cross-team IDOR rejection (highest value)
```csharp
// Illustrative — not from this repo
var otherMgr = new User { /* Role=Manager, different team */ };
store.Users[otherMgr.Id]=otherMgr;
var submitted = /* emp's expense moved to Submitted */;
Assert.Throws<ForbiddenException>(() => svc.Approve(otherMgr, submitted.Id));
```
Targets [`EnsureManagesOwner` L198-202](../../../backend/Services/ExpenseService.cs#L198-L202).

## Recipe 5 — Async side effect: receipt job sets state
```csharp
// Illustrative — not from this repo
var sp = BuildProviderWith(store);          // a ServiceProvider exposing DataStore
await BackgroundJobs.ProcessReimbursement(approvedExpenseId)(sp, CancellationToken.None);
Assert.Equal(ExpenseStatus.Reimbursed, store.Expenses[approvedExpenseId].Status);
Assert.StartsWith("PMT-", store.Expenses[approvedExpenseId].PaymentReference);
```
Targets [`ProcessReimbursement`](../../../backend/Background/BackgroundJobs.cs#L40-L69). Call
the delegate directly — no waiting on the real worker.

## Recipe 6 — Idempotency: reimbursing twice is safe
```csharp
// Illustrative — not from this repo
await job(sp, default); var ref1 = store.Expenses[id].PaymentReference;
await job(sp, default); Assert.Equal(ref1, store.Expenses[id].PaymentReference); // unchanged
```
Targets the guard at [`ProcessReimbursement` L51-56](../../../backend/Background/BackgroundJobs.cs#L51-L56).

## Recipe 7 — API integration: login → 401 without token
```csharp
// Illustrative — not from this repo
await using var app = new WebApplicationFactory<Program>();
var client = app.CreateClient();
var res = await client.GetAsync("/api/expenses");
Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
```
Requires making `Program` discoverable (`public partial class Program {}` at the end of
[Program.cs](../../../backend/Program.cs)) — a one-line, well-known tweak for `WebApplicationFactory`.

---

## Commands

```bash
dotnet test                                  # all
dotnet test --filter "FullyQualifiedName~Transitions"   # one
dotnet test --filter "Category=Authorization"           # by trait
```

**Drill:** implement Recipes 1 and 4 for real. They're the two highest-value tests in the
repo. Self-grade **Strong** if Recipe 4 fails *before* you'd trust any future refactor of
`ExpenseService`.
