# SEALED — attempt first. Answers to learn/00-onramp.md (.netramp)

Line numbers refer to `main` @ `c5ef2a7`. Runtime statements come from reading the code and documented framework
behavior. None of them was executed in this pass, and the ones that matter are marked UNVERIFIED.

## 1. Types
- **a) Doesn't compile.** `Id` is `{ get; init; }` (`Expense.cs:17`), so it can only be set in an object initializer.
  The compiler error is CS8852.
- **b) Compiles.** `IsEditable` is `false`, because `Expense.cs:44` is `Draft or Rejected` only.
- **c) The first line compiles; `ev.Note = ...` doesn't.** The properties of a positional record are `init`-only.
- **d) Compiles, with warning CS8602, and throws `NullReferenceException` at runtime.** `RejectionReason` is `null`
  until `Reject` sets it.
- **e) Compiles.** `with` creates a *new* record with only `Note` changed. `ev` itself is unchanged.
- **Why it matters:** the audit-trail type (`ExpenseEvent`) is immutable by construction. That is the one place this
  codebase gets immutability right, and Drill D contrasts it with the mutable `List<ExpenseEvent> History`.

## 2. State machine
| # | Answer | Why |
|---|---|---|
| a | true | line 24 |
| b | false | only `Manager` at line 32 |
| c | false | line 41 is an empty role array, so `.Contains(Finance)` is false |
| d | true | `CanSystemTransition` checks only that the edge exists (line 58) |
| e | **true** | the same edge-only check. `CanSystemTransition` is true for *human* edges too, so any code calling it bypasses roles for every edge. Keep this in mind for the review. |
| f | false | self-edges aren't in the table |
| g | 0 | `Reimbursed` isn't a key, so `NextStates` returns an empty array |
| h | false | `Rejected → Submitted` exists. Only `Reimbursed` is terminal (line 45). |

The empty array is there so the edge *exists* for `CanSystemTransition` and `NextStates`, while no role can pass
`CanTransition`. If the entry were missing, `BackgroundJobs.cs:51` would refuse to mark anything Reimbursed.

## 3. DI lifetimes
1. **`AddScoped<DataStore>()`.** Seeding writes into the scope created at `Program.cs:46-49`, and that store dies with
   the scope. The singletons `AuthService`, `PolicyEngine` and `ReceiptStorage` depend on `DataStore`, so each would
   capture a scoped service (a captive dependency). In `Development`, ASP.NET Core validates scopes and would throw at
   startup ("Cannot consume scoped service ... from singleton"). Outside Development, you'd get a split brain:
   `AuthService` keeps one store while each request's `ExpenseService` gets a fresh, empty one. So nobody can log in,
   and nothing persists between requests. UNVERIFIED (this is documented framework behavior; nobody ran it).
2. **Producers** are `ExpenseService.StartReimbursement` (`ExpenseService.cs:154`) and `ReceiptService.UploadAsync`
   (`ReceiptService.cs:52`). The **worker** is `QueuedHostedService` (`Background/QueuedHostedService.cs:24-52`), which
   drains the `Channel` in `BackgroundTaskQueue.cs`. If the queue were scoped, each request would write into its own
   private channel and the worker would never see the job.
   - Bonus observation: line 154 does **not** `await` `EnqueueAsync`, so the `ValueTask` is discarded. With a bounded
     channel in `Wait` mode (`BackgroundTaskQueue.cs:75-78`), a full queue means the reimbursement job can be silently
     lost or delayed while the API has already answered "Reimbursing".
3. `QueuedHostedService.cs:42` creates a fresh DI scope per job, and `scope.ServiceProvider` resolves the scoped
   `ExpenseService`. There is no `HttpContext` in a job, so anything that calls `IUserContext.RequireUser()` would throw
   there.

## 4. Request trace (Approve)
The request goes through these hops, in order:
1. `frontend/src/api/client.js:20-33` attaches `Authorization: Bearer <token>` and calls `fetch`. The Vite dev server
   proxies `/api` to the backend (`frontend/vite.config.js`).
2. `ExceptionHandlingMiddleware.InvokeAsync` (`Program.cs:52`), then `UseCors` (line 53).
3. `UseAuthentication` runs `TokenAuthenticationHandler.HandleAuthenticateAsync`
   (`Security/TokenAuthenticationHandler.cs:30-56`), which calls `AuthService.ValidateToken` (`AuthService.cs:41-53`)
   against `DataStore.Sessions` and builds claims, including `ClaimTypes.Role`.
4. `UseAuthorization` checks the class-level `[Authorize]` (`ExpensesController.cs:13`) and
   `[Authorize(Roles = "Manager")]` (line 80).
5. `ExpensesController.Approve` (line 81) calls `UserContext.RequireUser()`, which reads `NameIdentifier` and looks the
   user up in the store.
6. `ExpenseService.Approve` (`ExpenseService.cs:132`) runs `GetForActor` (`CanView`), then `EnsureManagesOwner`, then
   `Transition` (line 169).
7. `ExpenseStateMachine.CanTransition` runs, then `Status` is set, `Touch` runs, and `History.Add` records the event.
8. On the way back, `Mappings.ToResponse` builds the body and `Ok(...)` serializes it to JSON.

| Caller | Status | Where it's decided |
|---|---|---|
| a) no header | **401** | the handler returns `NoResult`, then `[Authorize]` challenges |
| b) Employee | **403**, empty body | the `Roles = "Manager"` check fails before the controller runs |
| c) Morgan approving the Draft | **422** ProblemDetails | Morgan manages Eli (`DataSeeder.cs:20`), so the view and owner checks pass. `Draft → Approved` isn't an edge, so a `DomainException` is thrown. |
| d) the wrong manager | **403** ProblemDetails | `CanView` is false, so `GetForActor` throws `ForbiddenException`. It's the same code as (b), but a different body shape, which is worth noticing when you debug. |
| e) Morgan approving the Submitted expense | **200** | the body's `status` is `Approved`, and `history` gains one entry |

## 5. Policy engine
- **a) Meals, $80, no receipt:** `RECEIPT_REQUIRED` (Blocking, since 80 > 25 and there's no receipt) and
  `OVER_CATEGORY_LIMIT` (Warning, since 80 > 75). `Submit` returns **422** with
  `title: "Business Rule Violation"`, `detail: "Expense violates policy and cannot be submitted."`, and
  `details: ["A receipt is required for amounts over <25 in the server culture's currency format>."]`. The `{:C}` format
  depends on the server culture, which is itself a small bug to notice.
- **b) Meals, $25, no receipt:** no flags. `25 > 25` is false, and 25 is under the $75 limit.
- **c) Software, $1,200, receipt Pending:** `RECEIPT_PROCESSING` (Warning), `OVER_CATEGORY_LIMIT` (Warning, since
  1,200 > 1,000) and `HIGH_VALUE` (Warning). There's nothing blocking, so it submits.
- **d) Travel, $0, whitespace description:** `AMOUNT_INVALID` and `DESCRIPTION_REQUIRED`, both Blocking. There's no
  receipt flag, because 0 > 25 is false and `ReceiptId` is null.

## 6. Errors to HTTP
1. The mapping is 404 / 403 / 422 / 500 (`ExceptionHandlingMiddleware.cs:94-100`).
2. The fixed string exists so that stack traces, internal messages and data values never reach the client. The full
   exception is logged at line 103 instead.
3. It would still wrap controller execution, but exceptions thrown earlier in the pipeline, by CORS or by the auth
   handler (for example inside `ValidateToken`), would bypass the ProblemDetails mapping. Registering it first means it
   wraps everything.

## 7. Characterization test (a reference; your version can differ)
```csharp
public class PolicyEngineBoundaryTests
{
    private static Expense Make(decimal amount, int daysAgo = 0) => new()
    {
        Amount = amount, Description = "x", Category = ExpenseCategory.Other,
        ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo)
    };
    private static IReadOnlyList<PolicyFlag> Eval(Expense e) => new PolicyEngine().Evaluate(e, hasProcessedReceipt: false);

    [Fact] public void Exactly25_NoReceipt_NotRequired() =>
        Assert.DoesNotContain(Eval(Make(25m)), f => f.Code == "RECEIPT_REQUIRED");

    [Fact] public void Over25_NoReceipt_RequiredAndBlocking() =>
        Assert.Contains(Eval(Make(25.01m)), f => f.Code == "RECEIPT_REQUIRED" && f.Severity == PolicySeverity.Blocking);

    [Theory, InlineData(90, false), InlineData(91, true)]
    public void StaleBoundary(int daysAgo, bool stale) =>
        Assert.Equal(stale, Eval(Make(10m, daysAgo)).Any(f => f.Code == "DATE_STALE"));
}
```
- **The clock:** the test builds dates relative to `DateTime.UtcNow`, just like the code does. It can still flake if
  it runs across UTC midnight, between building the expense and `Evaluate` reading the date. The deterministic
  refactor is to inject .NET 8's `TimeProvider` (or an `IClock`) into `PolicyEngine`, and pass a `FakeTimeProvider` in
  tests. That refactor belongs on a branch with its own tests, not on `main`. See
  [20 Money and time](../../../opusorganize/apprenticeship/curriculum/20-money-and-time.md).
- Using `Other` keeps the $500 category limit out of the way. Choosing inputs that isolate the rule under test is a
  habit worth building.

## 8. Seed guard
1. In practice it never fires. `Seed` runs once at startup against a new singleton. The guard only matters if `Seed` is
   called twice against the same store (for example from a future test fixture or a hot-reload path). It's a defensive
   guard.
2. **No, it wouldn't pass.** $420 with no receipt triggers `RECEIPT_REQUIRED` (Blocking). The seed sets
   `Status = Submitted` directly (`DataSeeder.cs:46`) instead of going through `ExpenseService.Submit`. So the seed
   produces a state the rules could never produce, and `PolicyFlags` stays empty until something calls
   `RefreshPolicy`.
   - Lesson: seed data and fixtures can quietly contradict your invariants. When a test "passes on seed data", ask
     whether that data could exist in production.
