# 03: Worked change: safe reimbursement admission

The behavior to repair is easy to reproduce. Create or seed an approved expense and use a bounded queue whose capacity is already consumed. The old `StartReimbursement` called `Transition` first and then called `_queue.EnqueueAsync(...)` **without awaiting it**, discarding the returned `ValueTask`.

Be precise about what that discarded `ValueTask` does, because the obvious guess is wrong. `BackgroundTaskQueue` is built with `FullMode = BoundedChannelFullMode.Wait` (`backend/Background/BackgroundTaskQueue.cs:27-29`, capacity 100 from `Program.cs`). On a **full** channel, `WriteAsync` does not drop the item and does not fail: it parks a pending writer that completes later, when a consumer frees capacity. So the old code did not silently lose the job — see `02-CONCEPTS.md` for the three `FullMode` options and `ExpenseWorkflowTests.Discarded_WriteAsync_on_a_full_queue_parks_and_lands_after_a_dequeue`, which demonstrates it on a capacity-1 queue.

The old code had three real defects, and none of them is "the item was dropped":

1. **The exception was lost.** An un-awaited `ValueTask` carries any failure (a cancelled or completed channel, an `ArgumentNullException`) into a task nobody observes. The request returns 200 and the failure disappears.
2. **Back-pressure was lost.** The whole point of a bounded channel is to make the producer feel the queue filling up. Discarding the `ValueTask` converts "wait until there is room" into "carry on immediately", and each blocked producer leaves a pending writer behind — an unbounded set of them, which is exactly the memory growth the bound was meant to prevent.
3. **The order was wrong.** `Transition` ran before admission was known, so the entity reached `Reimbursing` before anything had accepted responsibility for reimbursing it. If admission had thrown, or the process had stopped between the two statements, the expense would sit in `Reimbursing` forever with no job behind it.

The repair addresses all three at once by making admission and transition one command boundary, using the non-blocking `TryEnqueue` so the decision is known *before* any state changes:

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

Place this method in `backend/Services/ExpenseService.cs`, replacing the original method. The queue contract in `backend/Background/BackgroundTaskQueue.cs` must contain both methods because receipt upload still uses awaited `EnqueueAsync`:

```csharp
bool TryEnqueue(Func<IServiceProvider, CancellationToken, Task> work);
ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work);
```

The concrete queue uses `_channel.Writer.TryWrite(work)`. This is a synchronous channel operation and does not block while the expense gate is held. The order is deliberate: all preconditions are checked, the delegate is admitted, and only then is the state transition recorded. If admission returns false, nothing in the expense changed. If the worker begins immediately, it waits for the same gate.

The worker side is equally important. In `backend/Background/BackgroundJobs.cs`, the delay happens before the gate, then the job locks `store.GetExpenseLock(expenseId)`. It checks `CanSystemTransition` and only then writes the payment reference, status, timestamp, and event. A duplicate job sees `Reimbursed` and returns. The system edge helper is:

```csharp
public static bool CanSystemTransition(ExpenseStatus from, ExpenseStatus to) =>
    Transitions.TryGetValue(from, out var map)
    && map.TryGetValue(to, out var roles)
    && roles.Length == 0;
```

The focused test `Accepted_reimbursement_records_one_transition_and_duplicate_jobs_complete_once` uses the production handler with a zero delay overload, then invokes the same delegate twice. It asserts one `Reimbursed` event and a payment reference. `Full_reimbursement_queue_leaves_approved_expense_unchanged` uses a queue double that returns false from `TryEnqueue` and compares a snapshot of status, business fields, timestamp, policy count, and history count. This demonstrates the boundary at the caller’s actual entry point rather than testing a copied algorithm.

The queue remains in memory and the simulated delay remains a teaching stand-in. A durable payment outbox, retry policy, and database transaction are follow-up architecture work, outside this bounded repair.
