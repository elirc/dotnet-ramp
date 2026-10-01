# 05: Practice exercises

Work in a temporary branch or a copy of the project and run the focused test project after each exercise. Keep all fixtures inside a uniquely created temporary directory if you add file-backed experiments; delete that directory in a `finally` block. The existing tests use only memory and need no live service.

Exercise 0 — break it on purpose, before you read chapter 03. `astraupskill/snapshots/` holds the pre-change version of every file this delivery touched, and no exercise used them until now.

Start by trying the obvious thing: copy all five snapshots over their live files and run the tests. You will get compile errors, not failures — `error CS1061: 'BackgroundTaskQueue' does not contain a definition for 'TryEnqueue'` and `error CS1501: No overload for method 'ProcessReimbursement' takes 2 arguments`. That is worth seeing once, because it tells you something the prose cannot: this repair changed a *contract*, not just a method body. Restore the files.

Now reproduce the old **behaviour** while keeping the new shape. Three edits, all reversible:

1. `backend/Services/ExpenseService.cs`, `StartReimbursement` — put the transition first and fire the job and forget it, exactly as the original did:
   ```csharp
   Transition(expense, ExpenseStatus.Reimbursing, actor, note: "Reimbursement queued.");
   _ = _queue.EnqueueAsync(BackgroundJobs.ProcessReimbursement(expense.Id));
   ```
   (deleting the `if (!_queue.TryEnqueue(...)) throw ...` block).
2. Same file, `Reject` — assign the reason before proving the edge is legal:
   ```csharp
   expense.RejectionReason = normalizedReason;
   EnsureLegalHumanTransition(expense, ExpenseStatus.Rejected, actor);
   ```
3. `backend/Domain/ExpenseStateMachine.cs`, `CanSystemTransition` — drop the empty-role-array requirement, restoring `&& map.ContainsKey(to);`.

Run the suite:

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet test backend/ExpenseManagement.Api.Tests/ExpenseManagement.Api.Tests.csproj
```
```bash
DOTNET_ROLL_FORWARD=Major dotnet test backend/ExpenseManagement.Api.Tests/ExpenseManagement.Api.Tests.csproj
```

Observed with exactly those three edits:

```text
Failed!  - Failed: 4, Passed: 7, Skipped: 0, Total: 11 - ExpenseManagement.Api.Tests.dll (net8.0)
  Reject_on_draft_does_not_change_reason_history_or_timestamp            <- edit 2
  Full_reimbursement_queue_leaves_approved_expense_unchanged             <- edit 1
  Throwing_reimbursement_queue_leaves_approved_expense_unchanged         <- edit 1
  System_transition_only_allows_marked_worker_edge                       <- edit 3
```

Before you undo anything, answer three questions in writing.

- Edit 3 is the security one. Which *human* transition could a background delegate drive once `CanSystemTransition` accepts any legal edge, and who was supposed to be the only person able to make it? (Look at the role arrays in the transition table.)
- Edit 2 leaves a rejection reason on a **draft** expense. No exception was swallowed — `Transition` still threw and the request still failed. Where does the damage live, and which assertion in the test caught it?
- Edit 1 leaves the expense in `Reimbursing`. Re-apply edit 1 *only*, then re-run: which of the four failures come back and which do not? That is the difference between "I broke three things" and "I understand which test defends which line".

Then restore all three edits (`git restore backend/` if this is a git checkout, otherwise undo by hand) and confirm `Passed! - Failed: 0, Passed: 11` before continuing.

Exercise 1 — validation boundary. Extend the existing Create invalid-field test for `ExpenseService.ValidateAndNormalize` by calling `Create` with merchant text containing only spaces and asserts `DomainException`. Add a second case with `ExpenseCategory` cast from `999`. The answer should prove no expense was inserted. Use the existing `Fixture` and a count captured before the call. Do not rely on controller attributes because this is a direct service call.

Exercise 2 — rejection atomicity. Read `Reject` and explain why `ValidateReason` occurs before the lock. Add a test for a 501-character reason on a submitted expense and assert status, reason, history count, and timestamp remain unchanged. A complete setup uses `new string('x', 501)` and the production `ExpenseService.Reject` method. The expected failure is a `DomainException` with no event.

Exercise 3 — queue admission. Implement a `RecordingQueue` with `Accept = false`, call `StartReimbursement` for an approved expense, and assert `Work` is empty. Then create a second queue with `Accept = true` and a second service using the same store, call again, and assert one captured delegate and one `Approved -> Reimbursing` event. A third call using the accepting service should throw because the entity is already reimbursing; it must not capture another delegate.

Exercise 4 — worker authority. Call `BackgroundJobs.ProcessReimbursement(id, TimeSpan.Zero)` against an expense in `Submitted` and inspect the result. It should return without writing a payment reference or history event because `CanSystemTransition` rejects the human edge. Repeat with `Reimbursing` and verify one completion. The service provider needs `DataStore` and logging, as shown in `ExpenseWorkflowTests`.

Exercise 5 — review the contract. Explain why changing `TryEnqueue` to `async ValueTask<bool>` would force a different command design. Identify whether the HTTP controller would need to become async, and describe how you would test cancellation separately from queue-full admission. Keep the answer grounded in `BackgroundTaskQueue.cs`, `ExpensesController.cs`, and `QueuedHostedService.cs`.

Exercise 6 — from a lock to a transaction (the bridge exercise). This is the largest exercise in the lab and the one that connects it to the stack you will actually be hired to work on. Read the bridge table at the end of [02-CONCEPTS.md](02-CONCEPTS.md) first.

Design — and, if you have the time, build — an EF Core replacement for the expense half of `DataStore`:

1. **Schema.** An `Expense` table with a `[Timestamp] byte[] RowVersion`, an `ExpenseHistory` table with a foreign key to it, and an `OutboxMessage` table (`Id`, `Type`, `PayloadJson`, `CreatedAt`, `ProcessedAt` nullable).
2. **Replace the gate.** `StartReimbursement` becomes: open a transaction, load the expense, run the same `EnsureLegalHumanTransition` check, write the status change *and* an `OutboxMessage` describing the reimbursement job, commit. Delete the `lock` and the `GetExpenseLock` call — and notice that the lock-dictionary leak disappears with it, because the problem was self-inflicted.
3. **Handle the lost update.** Two Finance users click Reimburse at the same time. With `RowVersion`, the loser gets `DbUpdateConcurrencyException`. Decide whether your API reloads and retries or returns 409, and write down why. (Hint: reimbursement is not idempotent from the user's point of view; approving twice and paying twice are different kinds of wrong.)
4. **Deliver the outbox.** `QueuedHostedService` becomes a poller: read unprocessed `OutboxMessage` rows, run the work, mark them processed. State plainly what happens if the process dies *after* the work runs but *before* `ProcessedAt` is written, and what the job must therefore do when it sees an expense already in `Reimbursed` — which, happily, the current `BackgroundJobs.ProcessReimbursement` already does correctly via `CanSystemTransition`.

**Acceptance:** a written design naming every table, index and exception type; an explanation of which of the four current queue-related tests would still be meaningful and which would be replaced; and one sentence identifying the failure mode that `TryEnqueue` cannot fix but the outbox can. If you implement it, the original tests must still describe correct behaviour, even where their fixtures change.

If you only read one thing out of this exercise, make it this: the repair in chapter 03 is *correct* and is the right change for this codebase, and it is still not enough for a system that is allowed to crash. Knowing the difference between "my ordering is right" and "my ordering survives a power cut" is most of what separates a junior from a mid-level engineer on CRUD work.

For each exercise, write the assertion first, then implement the smallest production change that makes it pass. Record the command and test count in your local notes. A passing test that never reaches the production service is not evidence for this lesson.
