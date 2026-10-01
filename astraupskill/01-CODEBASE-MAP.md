# 01: Codebase map

The application’s request path is small enough to hold in your head. `backend/Program.cs` registers a singleton `DataStore`, singleton `PolicyEngine`, singleton `IBackgroundTaskQueue`, and scoped services such as `ExpenseService`. It also registers `QueuedHostedService`, which continuously dequeues delegates and executes each one inside a fresh dependency-injection scope. `backend/Controllers/ExpensesController.cs` translates HTTP routes into service calls. It obtains the authenticated `User` through `IUserContext`; authorization decisions still belong to the service.

The domain layer is in `backend/Domain`. `Expense.cs` is the mutable aggregate and owns `Status`, `PolicyFlags`, `History`, and timestamps. `ExpenseStateMachine.cs` is the legal-edge table. `Enums.cs` defines roles and statuses. `DomainExceptions.cs` defines the errors that middleware maps to user-facing responses. DTO records live in `backend/Dtos/Dtos.cs`; attributes help ASP.NET reject malformed HTTP payloads, while the service validates direct callers.

The command layer in `backend/Services/ExpenseService.cs` has a useful reading order. `Create` builds a draft and evaluates policy. `Update` edits an owned draft or rejected expense. `Submit` checks blocking policy, then uses `Transition`. `Approve` and `Reject` enforce manager ownership. `StartReimbursement` crosses the queue boundary. `RefreshPolicy` is also called from receipt processing. `Mappings.cs` computes available actions, so a frontend button is a projection of the same state and role rules.

The asynchronous path begins at `BackgroundTaskQueue.cs`. `EnqueueAsync` is appropriate for receipt work where waiting is acceptable. Reimbursement uses the new `TryEnqueue` method because admission must be a nonblocking command decision. `BackgroundJobs.ProcessReimbursement` waits for the simulated payment delay, obtains the expense gate, verifies the system-only edge, writes the payment reference, and appends one event. `QueuedHostedService` catches one job failure and continues the loop.

Trace this concrete scenario. A finance request reaches `ExpensesController.Reimburse`, calls `ExpenseService.StartReimbursement`, checks visibility and the Finance edge, admits a delegate, and records `Approved -> Reimbursing`. Later the hosted service invokes that delegate. The job waits, locks the same expense, sees `Reimbursing -> Reimbursed` is system-only, then records the payment. A second delegate sees `Reimbursed`, fails the system-edge check, and returns without a second payment or history item.

## The five expense commands, end to end

Keep this table open while you read chapters 02 and 03. Every row is one command; the columns are the boundaries a request crosses. All routes are under `[Route("api/expenses")]` with a class-level `[Authorize]`, so *authentication* is the controller's job and *authorization* — ownership, management relationship, legal edge — is the service's.

| Route | Controller action | Service method | Role gate | What is written on success | Queued job |
| --- | --- | --- | --- | --- | --- |
| `POST /api/expenses` | `Create` (`ExpensesController.cs:47-48`) | `ExpenseService.Create` | any authenticated user; the actor becomes the owner | new `Expense` in `DataStore.Expenses`, `Status = Draft`, initial `PolicyFlags` from `EvaluatePolicy`, first history entry | none |
| `PUT /api/expenses/{id}` | `Update` (`:55-56`) | `ExpenseService.Update` | owner only, and only while `Draft` or `Rejected` | normalized fields on the live entity, `UpdatedAt`, refreshed policy flags | none |
| `POST /api/expenses/{id}/submit` | `Submit` (`:71-72`) | `ExpenseService.Submit` | owner only | `Draft -> Submitted` transition, stored policy flags, one history event | none |
| `POST /api/expenses/{id}/approve` | `Approve` (`:79-81`) | `ExpenseService.Approve` | `[Authorize(Roles = "Manager")]` **and** the service proves this manager owns the employee | `Submitted -> Approved`, one history event | none |
| `POST /api/expenses/{id}/reject` | `Reject` (`:88-90`) | `ExpenseService.Reject` | same as approve | `RejectionReason` (validated *before* the lock), `Submitted -> Rejected`, one history event | none |
| `POST /api/expenses/{id}/reimburse` | `Reimburse` (`:97-99`) | `ExpenseService.StartReimbursement` | `[Authorize(Roles = "Finance")]` plus the Finance edge in the state machine | **admission first**, then `Approved -> Reimbursing` and one history event | `BackgroundJobs.ProcessReimbursement(id)` |

Only the last row crosses the queue boundary, and it is the only row where the order of two statements is the whole lesson. Note also what the table shows about role checks: the `[Authorize(Roles = ...)]` attribute answers "may a Manager call this endpoint at all", never "is this *the* manager of *this* employee". That second question is `ExpenseService`'s, which is why calling the service directly in a test is a legitimate way to exercise authorization — the attribute is not the rule.

The asynchronous half of the last row, once the delegate is dequeued:

| Step | Where | What it does |
| --- | --- | --- |
| 1 | `QueuedHostedService` | dequeues the delegate and opens a **fresh DI scope** for it — the request scope is long gone |
| 2 | `BackgroundJobs.ProcessReimbursement` | waits out the simulated payment delay **before** taking the gate, so a slow payment does not block commands |
| 3 | same method | locks `store.GetExpenseLock(expenseId)` — the *same* gate the command held |
| 4 | same method | checks `CanSystemTransition(Reimbursing, Reimbursed)`; a duplicate job finds `Reimbursed` here and returns |
| 5 | same method | writes the payment reference, status, timestamp and one history event |

For a first reading, open `Program.cs`, `ExpensesController.cs`, `ExpenseService.cs`, `ExpenseStateMachine.cs`, `BackgroundTaskQueue.cs`, and `BackgroundJobs.cs` in that order. Then open the actual regression cases in `backend/ExpenseManagement.Api.Tests/ExpenseWorkflowTests.cs` and match each assertion to a production line.
