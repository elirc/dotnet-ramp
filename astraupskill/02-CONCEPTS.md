# 02: Concepts behind the repair

Validation and authorization answer different questions. Validation asks whether the proposed data is structurally and semantically acceptable: trimmed merchant and description, amount from 0.01 through 1,000,000, a three-letter currency, a defined enum value, and a date that is not in the future. Authorization asks whether this actor owns the expense or manages its owner and whether their role can take the requested edge. `ExpenseService` performs both because its methods can be called without MVC model binding.

Failure-before-mutation is a state-management rule. Consider rejection. The old code assigned `RejectionReason`, then called `Transition`; if the expense was a draft, `Transition` threw and the reason remained on the draft. The repaired method trims and validates the reason, locks the expense, proves the manager relationship and legal edge, and only then assigns the reason. The same idea applies to update: normalize into local values before touching the aggregate.

A policy evaluation is a calculation until its result is committed. `EvaluatePolicy` returns flags based on the current expense and receipt status. `Submit` stores those flags only after proving the `Draft -> Submitted` edge and confirming that no flag blocks submission. If the policy blocks the command, the previous list, history, and timestamp remain stable. This distinction matters when UI policy text is also used as an audit-adjacent signal.

`ConcurrentDictionary` protects a dictionary operation such as `TryGetValue`; it does not protect `expense.Status = ...` followed by `expense.History.Add(...)`. `DataStore.GetExpenseLock` returns a stable gate per expense. Commands and the reimbursement worker lock that gate around related fields. Readers can still observe public mutable objects, so this is process-local coordination rather than a database transaction. A future database implementation would still need an atomic state update or optimistic concurrency token.

The queue boundary is a small state machine of its own. If a command waits on `EnqueueAsync`, a full queue can leave the request blocked. If it changes status first and later discovers a full queue, it leaves a false `Reimbursing` state. `TryEnqueue` returns an admission decision immediately. `StartReimbursement` checks the legal Finance edge, tries admission while holding the expense gate, then records the transition. The worker cannot take the gate until the transition is complete. No `.Wait()` or `.Result` is used under the lock.

## What a bounded channel actually does when it is full

You cannot reason about the queue boundary without knowing this, and guessing wrong is the most common mistake juniors make with `System.Threading.Channels`. When `Channel.CreateBounded` is given a capacity, `BoundedChannelOptions.FullMode` decides what a *writer* experiences once that capacity is used up:

| `BoundedChannelFullMode` | `WriteAsync` on a full channel | `TryWrite` on a full channel |
| --- | --- | --- |
| `Wait` (the default, and what this app uses — `BackgroundTaskQueue.cs:27-29`) | Does **not** complete. It parks a pending writer; the returned `ValueTask` completes when a reader frees a slot, and the item goes in then | Returns `false` immediately. Nothing is written |
| `DropWrite` | Completes immediately and **discards the new item** | Returns `true` — and still discards the item. This is the mode where "it returned success but nothing was queued" is real |
| `DropOldest` / `DropNewest` | Completes immediately, evicting an item that was already queued (or the incoming one) to make room | Returns `true`, with the same eviction |

Two consequences for reading the old `StartReimbursement`:

- **The job was never dropped.** Under `Wait`, the discarded `ValueTask` from `EnqueueAsync` eventually lands. `ExpenseWorkflowTests.Discarded_WriteAsync_on_a_full_queue_parks_and_lands_after_a_dequeue` proves this on a capacity-1 queue: `TryEnqueue` is refused while the channel is full, the parked writer's task is not complete, and after one `DequeueAsync` the item nobody awaited turns up in the queue. If you want a course anecdote about silently dropped work, `DropWrite` is where it lives — not here.
- **What *was* lost is the exception and the back-pressure.** A `ValueTask` nobody awaits carries its failure into nothing, and it converts "wait for room" into "return immediately and leave a pending writer behind". Unbounded pending writers defeat the purpose of the bound.

A related warning: a `ValueTask` may be awaited only once, and it is not safe to store, discard-then-inspect, or `Task.WhenAll` without calling `.AsTask()` first. The new test calls `.AsTask()` for precisely this reason. When in doubt, await it or convert it — never drop it.

## Known limitation: the lock dictionary grows forever

`DataStore.GetExpenseLock(id)` creates a gate object per expense ID and keeps it. Nothing removes the entry when the expense is deleted, so a long-running process accumulates one small object per expense ID it has ever seen. This is a real (small) leak introduced by the repair, and `VERIFICATION.md` discloses it. It is left in place deliberately: the fix is not free — you cannot simply remove the gate on delete, because a thread may be about to lock the object you are removing, and the next caller would then create a *different* gate for the same ID and the mutual exclusion would silently stop working. Treat it as a TODO with a design question attached, and see exercise 5. The database version of this problem does not exist at all, which is one of the better arguments for the next section.

System authority is explicit. Human edges have role arrays; `Reimbursing -> Reimbursed` has an empty role array. `CanSystemTransition` now requires that empty marker instead of treating every legal edge as a worker edge. That prevents a background delegate from silently approving or starting reimbursement.

The lock is also used to retrieve the current entity before `Update`, `Delete`, `Submit`, `Approve`, `Reject`, or reimbursement. This matters because a lookup performed before a competing delete can leave a detached object that still gets edited. Receipt upload and policy refresh remain a known boundary: receipt storage, replacement cleanup, and policy-list refresh can race with readers because the existing flow returns mutable objects. The course records that limitation rather than claiming the expense gate is a database transaction.

## Bridge: replacing the expense gate with a database

There is no EF Core, no `DbContext`, no migration and no SQL anywhere in this repository — `DataStore` is `ConcurrentDictionary` all the way down. That is a deliberate teaching simplification, and it is also the main thing you should not carry into a real codebase. Here is what each mechanism in this chapter becomes when the expenses live in a database.

| Here | With EF Core | Why it is better |
| --- | --- | --- |
| `lock (_store.GetExpenseLock(id))` around a read-modify-write | `await using var tx = await db.Database.BeginTransactionAsync();` … `await tx.CommitAsync();` | Works across processes and machines. The gate only coordinates threads inside one process, so two instances of this API behind a load balancer would both "hold the lock" |
| The gate dictionary that never shrinks | nothing — there is no per-entity object to keep | The leak described above is an artefact of doing locking in the application. The database has no equivalent |
| The expense object mutated in place, visible to every reader immediately | A tracked entity whose changes land on `SaveChangesAsync`, invisible to other transactions until commit | Readers stop seeing half-applied commands. Today `ExpenseService` can be mid-`Transition` while another request reads `expense.Status` — disclosed in this chapter as "readers can still observe public mutable objects" |
| Nothing prevents a lost update between two Finance users | `[Timestamp] public byte[] RowVersion { get; set; }` on `Expense`, producing `DbUpdateConcurrencyException` | The second writer is told it lost, instead of silently overwriting. You then choose: reload and retry, or return `409 Conflict` |
| `expense.History.Add(...)` in the same `lock` as the status write | Both in the same `SaveChangesAsync` — one `INSERT` and one `UPDATE` in one transaction | Atomicity for real: no history row can survive a rolled-back status change |
| `TryEnqueue` before the transition, so a full in-memory queue cannot leave a false `Reimbursing` | A **transactional outbox**: write the status change *and* an `OutboxMessage` row in the same transaction, and let a separate process deliver it | Survives a crash. `TryEnqueue` solves "the queue is full"; it does not solve "the process died between `Transition` and the job running" |

That last row is the important one and it is the honest limit of this course's repair. Ordering `TryEnqueue` before `Transition` removes a *rejection* race — a queue that refuses admission can no longer leave an approved expense falsely reimbursing. It does nothing about a crash. If the process stops immediately after `Transition` returns, the in-memory queue dies with it and the expense is stranded in `Reimbursing` with no job, exactly the state the repair was meant to prevent. An outbox row in the same transaction as the status change is the only thing that closes that hole, and it is why "just use a background queue" is not a complete answer in production systems.

Exercise 6 in chapter 05 makes you design this change; chapter 06 reviews it.
