# 07: Trace lab: finance reimbursement

Use this trace table while reading the code. The trigger is `POST /api/expenses/{id}/reimburse`; the request has a valid bearer token for a Finance user.

| Step | Actual location | State or decision | Observable effect |
| --- | --- | --- | --- |
| 1 | `Controllers/ExpensesController.cs`, `Reimburse` | `_userContext.RequireUser()` resolves Finance | Unauthenticated or missing users fail before service work |
| 2 | `DataStore.GetExpenseLock` | Per-expense gate is acquired before lookup | Expense commands and reimbursement worker serialize their changes |
| 3 | `ExpenseService.GetForActor` | Current expense exists and Finance can view it | Missing or already-deleted id is `NotFoundException` |
| 4 | `EnsureLegalHumanTransition` | `Approved -> Reimbursing` is allowed for Finance | Wrong state or role throws without mutation |
| 5 | `IBackgroundTaskQueue.TryEnqueue` | Delegate is admitted immediately or returns false | Full queue throws while expense remains Approved |
| 6 | `Transition` | Status and audit event become Reimbursing | One human event has note “Reimbursement queued.” |
| 7 | `QueuedHostedService` | Delegate runs in a fresh scope | A job exception is logged and loop continues |
| 8 | `BackgroundJobs.ProcessReimbursement` | Delay simulates payment work | No expense mutation happens during the delay |
| 9 | Worker lock and `CanSystemTransition` | Only Reimbursing -> Reimbursed is system-only | Submitted or Approved cannot be completed by a worker |
| 10 | Worker mutations | Payment reference, status, timestamp, event are written | One Reimbursed event is produced |

Now answer three questions. Where would a queue-full failure appear to the controller? It is a `DomainException` mapped by middleware, with no false success response. Why does the worker lock after its delay? The delay does not need serialization; only the read-check-write completion does. What happens on a duplicate job? It finds `Reimbursed`, so `CanSystemTransition` returns false and it exits without another payment reference or event.

Compare the trace with the actual `Accepted_reimbursement_records_one_transition_and_duplicate_jobs_complete_once` test. The test uses the service entry point for admission and the real handler delegate for completion. It intentionally invokes the delegate twice and asserts a positive history count, a `PMT-` reference, two total events, and one terminal event. That is a compact executable version of this table.

For a debugging variation, put a breakpoint or log around `TryEnqueue`, then run the full queue test. The correct order is precondition check, queue admission, transition, return. No async wait occurs under the gate.

The test invokes two duplicate jobs with a zero delay. Task.WhenAll collects their tasks, but those delegates can finish synchronously before the second invocation starts; this is duplicate-execution evidence, not a forced overlapping-worker test. The lock is visible in production source, while a controlled two-thread test would be required to demonstrate a particular interleaving. Receipt uploads and direct mutation through returned entities do not participate in this coordination.

## Steps 2 to 5 in one method

Rows 2-5 of the table are a single service method, from `backend/Services/ExpenseService.cs:187-201`. Read the order: the per-expense gate, then the visibility check, then the transition rule, then the queue admission — the status only changes after the job is accepted.

```csharp
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
```

