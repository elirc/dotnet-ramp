# Side Effects, Async & Reliability

## The two background flows

| Flow | Trigger | Job | Effect |
| --- | --- | --- | --- |
| Receipt scan/OCR | upload | [`ProcessReceipt` L16-33](../../../backend/Background/BackgroundJobs.cs#L16-L33) | sets `ExtractedAmount`, status=Processed |
| Reimbursement payment | finance action | [`ProcessReimbursement` L40-69](../../../backend/Background/BackgroundJobs.cs#L40-L69) | sets payment ref, status=Reimbursed |

## The mechanism

A bounded `Channel<Func<…>>` is the queue
([`BackgroundTaskQueue` L15-37](../../../backend/Background/BackgroundTaskQueue.cs#L15-L37));
a `BackgroundService` is the consumer
([`QueuedHostedService` L24-52](../../../backend/Background/QueuedHostedService.cs#L24-L52)).
Producers `EnqueueAsync` a delegate; the worker dequeues, opens a **fresh DI scope**, and
runs it. This is the standard ASP.NET Core in-process background pattern.

**Three things it gets right:**
1. **Backpressure** — `BoundedChannelFullMode.Wait`
   ([L22-25](../../../backend/Background/BackgroundTaskQueue.cs#L22-L25)) makes producers wait
   if the queue is full, instead of unbounded memory growth.
2. **Error isolation** — a failing job is caught and logged; the loop continues
   ([L45-48](../../../backend/Background/QueuedHostedService.cs#L45-L48)). One poison job
   can't take down processing.
3. **Scope-per-job** — avoids the captive-dependency bug and mimics a per-request scope.

## Where reliability breaks down (the senior view)

This is an **in-process, in-memory, at-most-once** queue. Be explicit about what that means:

| Property | Status | Consequence |
| --- | --- | --- |
| Durability | ❌ none | Process crash loses all queued + in-flight jobs |
| Retries | ❌ none | A transient failure (or a thrown OCR) leaves the entity stuck |
| Idempotency | ⚠️ partial | `ProcessReimbursement` re-checks `CanSystemTransition` ([L51](../../../backend/Background/BackgroundJobs.cs#L51)) so a double-run is safe; `ProcessReceipt` has no such guard |
| Delivery | at-most-once | Enqueue isn't transactional with the state change |
| Visibility | ⚠️ logs only | No metric/alert for "stuck in Reimbursing" |

**The crash window (worth internalizing):** in
[`StartReimbursement` L153-154](../../../backend/Services/ExpenseService.cs#L153-L154), the
status flips to `Reimbursing` and *then* the job is enqueued (not awaited, not transactional).
If the process dies in between, the expense is `Reimbursing` forever — no payment, no retry,
no recovery. This is the canonical motivation for the **outbox pattern**: write the state
change and an "outbox" record in one transaction; a separate dispatcher reads the outbox and
performs the side effect with retries. It converts at-most-once into at-least-once + idempotent.

## Idempotency, explained via this code

`ProcessReimbursement` is **idempotent by guard**: if it runs twice, the second run sees the
status is already `Reimbursed`, fails the `CanSystemTransition` check, logs a warning, and
returns without double-paying ([L51-56](../../../backend/Background/BackgroundJobs.cs#L51-L56)).
That's a concrete, copyable example of "make the side effect safe to repeat." Contrast
`ProcessReceipt`, which would happily reprocess and re-stamp `ExtractedAmount` — harmless
here, but the asymmetry is a good discussion.

## What you'd add to make it production-grade

1. Persist the queue (DB table or a broker) → survive restarts.
2. Outbox for state-change-plus-side-effect atomicity.
3. Retry with backoff + a dead-letter path for poison jobs.
4. A "stuck" detector: a periodic scan for `Reimbursing` older than N minutes → alert.
5. Metrics: queue depth, job duration, failure count (see [05/06 observability](../05-quality-engineering/06-observability-and-operations.md)).

**Drill:** Pick the reimbursement flow and write the 5-step outbox redesign as a short RFC
(template in [07/02](../07-career-and-collaboration/02-writing-prs-and-rfcs.md)). State what
you'd test and how you'd roll it out behind the existing API without breaking the SPA.
