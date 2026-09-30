# 18. Durable handoff and an outbox design studio

This module proposes a durable successor to the current reimbursement handoff in [ExpenseService](../../backend/Services/ExpenseService.cs), [BackgroundTaskQueue](../../backend/Background/BackgroundTaskQueue.cs), and [BackgroundJobs](../../backend/Background/BackgroundJobs.cs). The current app uses in-memory state and simulated payment work. Nothing in this chapter claims that a database outbox or external payment integration already exists.

## Define acceptance before choosing storage

The first design question is what a successful reimbursement request promises. It could promise only that the expense entered Reimbursing in the current process, that an in-memory queue accepted a delegate, or that durable work exists and will be retried according to a stated policy. These are progressively stronger contracts.

The current service changes the expense and invokes enqueue without observing completion. The queue lab proves that acceptance can remain pending under capacity pressure. The service lab demonstrates the resulting observation gap with a controlled queue. A durable design should explicitly choose a stronger acceptance point rather than merely add await and call the problem solved.

For this studio, choose the proposed contract: a successful request means the expense transition and a durable reimbursement-work record committed together. It does not mean payment completed. The response should identify the accepted work and expose a status that can later be queried.

## Model the local transaction

A proposed transaction reads the expense at an expected version, verifies Finance authority and the Approved-to-Reimbursing edge, assigns the new status, appends an accepted transition event, inserts a work record, and commits. Either all those local changes commit or none do under the chosen persistence mechanism.

The work record might contain work ID, expense ID, operation type, accepted version, created time, attempt count, state, and next-attempt time. Avoid embedding a live delegate or service object in durable storage. Persist data that a worker can interpret after process restart.

A unique command identity can prevent the same accepted request from creating multiple work records. Define its scope and payload semantics. Reusing a key for a different expense or changed command should have an explicit response rather than accidentally replaying unrelated work.

## Work through the crash windows

Crash before commit leaves no accepted transition or work record under the proposed transaction contract. The client may retry and create the first accepted command. Crash after commit but before response leaves accepted work even though the client does not know the result. A retry with the same identity should find and return the committed outcome rather than enqueue another business operation.

Crash after the worker claims work but before performing the external effect requires claim recovery. A lease can expire and allow another worker to continue. Crash after an external effect but before marking work complete is harder: retry may repeat the external call unless the external system or local protocol supports idempotent reconciliation.

Do not hide the last window behind the phrase exactly once. Local transaction atomicity cannot by itself make an external effect and a database commit one indivisible action. The design must explain duplicate suppression, external operation identity, or reconciliation of uncertain outcomes.

## The current payment is simulated

BackgroundJobs currently generates a local payment reference after a delay and mutates the in-memory expense. There is no selected external provider call in that path. A design studio can introduce a hypothetical provider interface, but every provider behavior must be stated as an assumption.

For example, assume a provider accepts a stable operation ID and returns the same result for repeated calls with that ID. Then the worker can reuse the work ID as a provider idempotency identity if the provider's scope and lifetime permit it. If the provider lacks that guarantee, a different reconciliation strategy is required.

Do not invent provider pricing, legal requirements, or current API behavior for this exercise. Use a synthetic provider double with declared semantics. The educational goal is to reason about the protocol, not to pretend an integration has been researched or tested.

## Claiming work safely

A worker should claim a pending record with an atomic state or version condition. Two workers reading the same pending row must not both believe they exclusively own it without a defined duplicate-safe execution strategy. The claim can include owner ID and lease expiry, but those fields need update rules and clock assumptions.

A lease is not a proof that a slow worker stopped. If its lease expires while it still executes, another worker can begin. A fencing token or idempotent external operation can prevent stale owners from producing conflicting effects under the proposed design. Choose the mechanism according to the resource being protected.

For the educational model, use integer lease generations rather than real machine clocks at first. This makes stale-owner rejection easy to predict. Later add time-based expiry with an injected clock and explicit skew assumptions if the deployment requires it.

## Retry classification matters

Not every failure should be retried identically. A transient transport error, invalid expense state, permanent provider rejection, and unknown external outcome require different handling. The work record should retain a sanitized category and enough correlation information to support recovery without storing sensitive payloads in logs.

A retry policy needs a maximum or escalation rule, delay strategy, and operator-visible terminal state. Endless immediate retries can overload a dependency and obscure the underlying issue. Conversely, marking every exception permanently failed can abandon recoverable work.

The acceptance contract should tell the user what pending, retrying, completed, and needs-review mean. The detail page can then poll or subscribe to a meaningful work status instead of inferring everything from an expense status that may be stale or incomplete.

## Completion must preserve aggregate invariants

When a worker obtains a successful synthetic provider result, the proposed completion transaction should verify the work identity and expected expense relationship before assigning PaymentReference, Reimbursed status, timestamp, and completion event. Replaying the same completion should not append another event or replace a different operation's reference silently.

A version conflict at completion does not automatically mean payment should be repeated. The external effect may already exist. Reconciliation should compare the work record and provider operation identity, then decide how to update local state safely. This is another reason to separate command attempts from business effects.

If the expense no longer exists or is in an unexpected state, record a distinct recovery outcome. The current worker logs and returns for some such cases; a durable design should make the unresolved work visible enough that it does not disappear from operational attention.

## A testable outbox model

Build a small educational model with expense state, work records, claim generations, and a provider double. Inject crashes at five points: before local commit, after local commit, after claim, after provider success, and after completion commit. For each, restart the model and apply the recovery procedure.

The oracle should assert one accepted command identity, no missing work after accepted response, at most one provider effect under the declared idempotency assumption, one completion event, and a truthful final status. If the provider assumption is removed, the model should expose an unknown-outcome state rather than falsely retain the at-most-one guarantee.

This model would be a proposed next lab, not an executed result in the current report. Its value is the explicit fault matrix and independent acceptance criteria. A later real database test must verify transaction and claim semantics against the chosen adapter.

## Independent exercises

Exercise NR18-A: define the successful-request acceptance point and draw the local transaction boundary. Compare it with both current service invocation and awaited in-memory acceptance.

Exercise NR18-B: complete the five-crash matrix and explain the external-effect uncertainty window. State the exact synthetic provider assumption needed for safe replay.

Exercise NR18-C: design claim, retry, completion, and needs-review states with a stale-owner case. Give an oracle that would fail if a completed work replay appended a second business event.
