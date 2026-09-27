# On-ramp: 8 warm-ups on .netramp

These come before any `training/` material. Each one uses a real file from this repo, cited as `path:line` against
`main` @ `c5ef2a7`. Write your answer *before* checking `learn/_answers/00-onramp.md`.
Time: about 2.5 hours total. Exercises 1–6 need only reading. Exercise 7 needs the test project from Session 3.

---

## 1. Types: what compiles, what doesn't (10 min)
Read `backend/Domain/Expense.cs` (45 lines). Given `var e = new Expense();`, which of these lines compile? For the ones
that compile, what value do they produce?

```csharp
a) e.Id = Guid.NewGuid();
b) e.Status = ExpenseStatus.Submitted;  bool x = e.IsEditable;
c) var ev = new ExpenseEvent(ExpenseStatus.Draft, ExpenseStatus.Submitted, Guid.Empty, "System", null, DateTimeOffset.UtcNow);
   ev.Note = "changed";
d) string? r = e.RejectionReason;  int len = r.Length;
e) var copy = ev with { Note = "changed" };
```

## 2. Predict the state machine (15 min)
Read `backend/Domain/ExpenseStateMachine.cs:19-58`. Predict `true` or `false` for each of these. Don't run anything.

| # | Call |
|---|---|
| a | `CanTransition(Draft, Submitted, Employee)` |
| b | `CanTransition(Submitted, Approved, Finance)` |
| c | `CanTransition(Reimbursing, Reimbursed, Finance)` |
| d | `CanSystemTransition(Reimbursing, Reimbursed)` |
| e | `CanSystemTransition(Submitted, Approved)` |
| f | `CanTransition(Approved, Approved, Manager)` |
| g | `NextStates(Reimbursed).Count` (give a number) |
| h | `IsTerminal(Rejected)` |

Then answer: why is the `Reimbursing` entry at line 41 an *empty array* rather than just missing from the table?

## 3. DI lifetimes (15 min)
`backend/Program.cs:16-26` registers 4 singletons, 1 hosted service and 4 scoped services.
1. `DataStore` is a singleton. Describe exactly what users would see if someone changed line 16 to `AddScoped<DataStore>()`.
   Hint: look at where seeding happens, at lines 46-49.
2. The comment at line 15 says the queue "must also be a singleton so producers and the worker share it." What is the
   producer here, and what is the worker? Name the files.
3. `ExpenseService` is scoped, but the background job uses it (`backend/Services/ReceiptService.cs:52-58`). Where does
   the job get an `ExpenseService` from, given that no HTTP request is in flight? Read
   `backend/Background/QueuedHostedService.cs` to answer.

## 4. Trace one request end to end (20 min)
A manager clicks "Approve". Starting from `frontend/src/api/client.js:20-45`, list every file and method the request
passes through until `expense.Status` changes, and then back until the response is sent. You should end up with at
least 7 hops.

Then predict the HTTP status code for each of these callers:
- a) no `Authorization` header;
- b) Eli Employee (`employee@demo.com`, see `DataSeeder.cs:20`);
- c) Morgan Manager, approving the seeded **Draft** expense;
- d) a manager whose `Id` isn't Dana's `ManagerId`, approving Dana's Submitted expense. There's no such manager in the
  seed data, so imagine one.
- e) Morgan Manager, approving the seeded Submitted expense.

## 5. Policy engine by hand (15 min)
`backend/Services/PolicyEngine.cs:34-66`. Assume today's date is the expense date, and give the list of flag codes and
their severities for each case:
- a) Meals, $80.00, no receipt;
- b) Meals, $25.00, no receipt;
- c) Software, $1,200.00, receipt attached but still `Pending`;
- d) Travel, $0.00, description `"   "`.

For case (a), what HTTP status and body shape does `POST /api/expenses/{id}/submit` return? Follow
`ExpenseService.Submit` (lines 114-130) into `ExceptionHandlingMiddleware.cs`.

## 6. Where errors become HTTP (10 min)
Read `backend/Middleware/ExceptionHandlingMiddleware.cs`.
1. What status does each of `NotFoundException`, `ForbiddenException`, `DomainException` and a plain
   `NullReferenceException` produce?
2. Why does the 500 case replace `Detail` with a fixed string? What would leak if it didn't?
3. `Program.cs:52` registers this middleware *first*. What would change if it were registered after `UseAuthorization()`
   (line 55)?

## 7. Your first characterization test (30 min, needs the test project from Session 3)
Write xUnit tests that pin the **current** behavior of `PolicyEngine.Evaluate` at its boundaries:
- `Amount = 25m`, no receipt → no `RECEIPT_REQUIRED` flag.
- `Amount = 25.01m`, no receipt → `RECEIPT_REQUIRED` with severity Blocking.
- `ExpenseDate` exactly 90 days ago → no `DATE_STALE`. 91 days ago → `DATE_STALE`.

Constraint: `PolicyEngine` reads `DateTime.UtcNow` (line 37). Explain how your test copes with that, and name the
refactor that would make it deterministic. **Don't make that refactor on `main`.**

## 8. Read the seed guard (10 min)
`DataSeeder.Seed` starts with `if (!store.Users.IsEmpty) return;` (line 16).
1. When would that early return actually fire in this app?
2. The two seeded expenses have no receipts. Expense 2 is $420 of Travel. Using exercise 5, would it pass `Submit` today
   if it were back in Draft? What does that tell you about seed data versus the rules?
