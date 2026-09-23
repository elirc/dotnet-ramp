# TECHNICAL_QUESTIONS — ExpenseManagement (.netramp)

All 10 questions are specific to this codebase. Answer from the actual source,
not from general knowledge of the pattern being asked about.

1. **API design.** `ExpensesController.Reimburse` is `[Authorize(Roles =
   "Finance")]` and calls `ExpenseService.StartReimbursement`, which calls
   `ExpenseStateMachine.CanTransition` before transitioning. Given incident 001
   (the fast-track bypass), where should the authority to allow/deny a
   transition actually live — the controller's role attribute, the service, or
   the state machine table — and why does having it in more than one place
   *not* automatically make the system safer? Cite the specific place a
   duplicate check would have to live to catch what the incident 001 diff
   missed.

2. **API design.** `ExpensesController.Get`, `.Update`, `.Delete` all take a
   bare `Guid id` in the route and rely entirely on `ExpenseService` to enforce
   ownership (`GetOwned`, `CanView`). Walk through what HTTP status code an
   Employee gets back if they request another employee's expense ID directly,
   and trace exactly which line produces it (`Middleware/
   ExceptionHandlingMiddleware.cs` + the exception type thrown in
   `ExpenseService.cs`).

3. **Data modeling.** `DataStore` is `ConcurrentDictionary<Guid, Expense>` with
   no foreign-key enforcement. If this were ported to EF Core + Postgres (which
   the README explicitly frames as the expected next step), which two
   relationships in this domain most need a real foreign key with `ON DELETE`
   behavior specified, and what should that behavior be for each (cascade,
   restrict, or set null) — reference `Expense.EmployeeId`, `Expense.ReceiptId`,
   and `Receipt.ExpenseId` specifically.

4. **Data modeling / concurrency.** `Expense.History` is a `List<ExpenseEvent>`
   mutated in place by `ExpenseService.Transition`. In a real database, would
   you model the audit trail as a column on the `Expense` row (JSON list) or as
   its own `ExpenseEvent` table? Argue both ways using this app's actual access
   patterns (how `History` is read in `Mappings.ToResponse` vs. how Audit's
   incident-001 investigation query — "every `Reimbursed` expense lacking an
   `Approved` event" — would need to run).

5. **React / testing.** There are zero test files in this repo (`backend/` and
   `frontend/` both). If you had to add exactly one test today with the
   highest bug-catching value per line of test code, which class or component
   would you pick and why (not "add tests everywhere" — name the one)?

6. **Testing.** Explain the specific difference between the characterization
   test you wrote for `ExpenseStateMachine` (`training/review/
   CHARACTERIZATION_TEST_EXERCISE.md`) and a normal unit test suite for the
   same class. What would you have done differently if you were asked to write
   a *correctness* test suite instead (i.e., you got to decide what the state
   machine *should* allow, not just record what it does)?

7. **Security.** `AuthService.Login` (`Security/AuthService.cs:26-30`) has a
   comment claiming constant-time password verification. Explain exactly why
   the `||` short-circuit breaks that claim for one specific input, and
   describe the minimal code change that would make the comment true again
   (hint: think about what has to happen even when the user doesn't exist).

8. **Security / file handling.** `ReceiptStorage.OpenRead` builds `fullPath =
   Path.GetFullPath(Path.Combine(_root, receipt.StoredFileName))` and checks
   `fullPath.StartsWith(Path.GetFullPath(_root))` before serving the file. Given
   that `StoredFileName` is always server-generated (`$"{Guid.NewGuid():N}
   {extension}"`), is this check dead code, defense-in-depth, or a bug waiting
   to happen? Justify your answer by describing a change elsewhere in the
   codebase that would make this check load-bearing.

9. **Operability.** Name the two resource-leak findings from the sealed review
   (`training/_answers/review.md` findings #8 and #9) and, for each, describe
   what monitoring signal would let an on-call engineer notice the leak in
   production *before* it caused an outage (disk full / OOM), given that
   neither leak throws an exception or logs an error when it happens.

10. **Background jobs / idempotency.** `ReceiptService.UploadAsync` enqueues a
    background job that both processes the receipt scan/OCR *and* (inline, in
    the same closure) calls `ExpenseService.RefreshPolicy` on completion
    (`Services/ReceiptService.cs:52-58`). If `IBackgroundTaskQueue.EnqueueAsync`
    succeeded but the process crashed before the queued job ran (e.g. the app
    restarts while a receipt is `Pending`), what state is the expense left in,
    and what would a user see if they tried to submit it? Is there any recovery
    path in the current code, or would that receipt be stuck `Pending` forever?
