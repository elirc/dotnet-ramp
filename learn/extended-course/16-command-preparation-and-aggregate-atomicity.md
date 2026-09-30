# 16. Command preparation and aggregate atomicity

This is a design module grounded in [ExpenseService](../../backend/Services/ExpenseService.cs), [Expense](../../backend/Domain/Expense.cs), and [the state table](../../backend/Domain/ExpenseStateMachine.cs). It proposes a stronger command boundary without changing application code. The goal is not to add an abstract architecture for its own sake. It is to make one precise guarantee reviewable: a rejected command should not leave unintended partial aggregate changes.

## Start with the observed counterexample

Reject assigns RejectionReason before Transition checks the lifecycle edge. Submit replaces PolicyFlags before policy and lifecycle rejection. These are different forms of partial change. The first changes command data before a rejected transition; the second updates derived validation state during an attempted command.

A design must decide which changes are permitted. If policy flags are treated as a fresh observation that may update independently, the rejection contract can allow that specific field while protecting lifecycle and command fields. If the aggregate must be unchanged on every rejected command, policy evaluation must also be separated from assignment. Do not use the same phrase atomic command for both contracts without naming the difference.

Write a field inventory before proposing code. Include merchant, description, amount, currency, category, expense date, status, receipt reference, reason, payment reference, policy flags, history, and timestamps. Identify fields read by the command, fields proposed for change, and fields outside its scope. This makes accidental broad mutations visible.

## Separate preparation from application

A proposed PrepareReject operation can resolve the actor and expense, check visibility and direct-report authority, normalize the reason into a local value, verify the transition, and construct the intended event without mutating the expense. An ApplyPreparedChange operation can then assign the reason, status, timestamp, and event together under the chosen synchronization boundary.

Preparation alone does not solve concurrency. If another writer changes the expense after preparation but before application, the prepared result may be stale. The proposal therefore needs either a lock covering preparation and application, a version checked at application, or an immutable replacement protocol with an atomic compare. State which mechanism the design assumes.

For a single-threaded educational model, it is acceptable to demonstrate preparation and application sequentially. But label that model's concurrency assumption. The actual application has a background worker and mutable shared objects, so a production-strength claim requires a stronger boundary than sequential pseudocode.

## A worked prepared rejection

Take expense E at version seven in Submitted, owned by Erin, with manager Morgan, old reason absent, and two history events. Prepare a rejection with reason containing surrounding spaces. The preparation result can contain expected version seven, destination Rejected, normalized reason, one captured time, and one event from Submitted to Rejected.

Before application, verify that E still has version seven and status Submitted. If so, apply the fields, append the event, and advance version to eight under the proposed protocol. If another operation already changed E, reject the prepared change without assigning the new reason or event. Version is a proposed field here; it is not present in the current Expense class.

This trace gives an independent oracle. A stale prepared change must leave every protected field unchanged. A successful change must produce exactly one new event whose from and to values match the actual accepted transition. Capturing one time can make UpdatedAt and event time intentionally equal if that is the chosen contract.

## Normalization is not validation

Trimming a reason produces a normalized value but does not necessarily make it acceptable. An all-whitespace reason becomes empty. The HTTP RejectRequest has required and length metadata, while direct service calls can bypass that validation. A proposed domain command should decide whether nonempty normalized reason is an invariant regardless of entry point.

The same distinction applies to currency and merchant. Current Create trims merchant and description but uppercases currency without trimming. A new value object could enforce a canonical currency representation, but that changes accepted inputs and possibly stored data. List those changes explicitly rather than smuggling them into a refactor described as behavior-preserving.

A useful exercise separates normalization failures from authority failures. The order can affect error messages and information disclosure. Choose an order that serves the product contract, then test it. Do not let incidental helper order become an undocumented policy.

## History should describe accepted changes

If the prepared event is appended only after acceptance, rejected commands produce no transition event. That does not mean failed attempts must be invisible operationally. A separate diagnostic or audit stream can record denied attempts without pretending the business state changed.

Keep business history and operational attempts distinct. Expense.History currently represents transition events shown to users. Adding failure events with the same from and to schema can confuse reports and UI unless the event model is extended deliberately. A proposal should define event type, actor, reason, timestamp, and visibility for each category.

An event should also have a stable identity if persistence or deduplication is introduced. The current record has no explicit event identifier. A proposed event ID can support retry-safe append and correlation, but it requires serialization and storage changes that belong in the migration plan.

## Encapsulation can enforce the protocol

A stronger aggregate could expose methods rather than public setters for protected state. That reduces the number of writers able to bypass the transition table. However, it also affects JSON serialization, fixture construction, background jobs, and store replacement. A design review should inventory those callers before changing accessibility.

One incremental approach keeps external request DTOs separate and introduces internal command methods while leaving read projections stable. Another uses immutable aggregate snapshots and replaces them in the store. Both can work, but they have different performance and synchronization consequences.

Do not assume immutable records automatically make nested lists immutable. If history or policy flags remain mutable collections referenced by several snapshots, an apparent copy can still share changing state. An unchanged-input test should inspect collection contents and ownership, not only the outer object reference.

## Define rejection snapshots precisely

A snapshot can serialize selected values into a stable comparison structure, copy arrays of event records, and capture scalar timestamps. Avoid comparing references to the same mutable list before and after; both references can observe the final state and hide a mutation.

Also distinguish semantic equality from representation identity. A policy refresh may replace a list with equal contents. If the contract protects only visible values, that replacement may be acceptable. If downstream observers depend on reference identity or notification behavior, the contract is broader. State the chosen level so tests do not accidentally overconstrain implementation.

A good test matrix includes invalid state, invalid actor, invalid normalized input, stale version, and a collaborator failure before application. Each should identify the first rejecting boundary and the protected fields. A successful case should verify the exact changed fields and confirm unrelated fields remain stable.

## Independent extension exercise

Propose a rule that a rejection reason must include at least a small meaningful explanation after trimming. Choose a concrete minimum appropriate for the exercise and label it as proposed rather than existing policy. Add boundary cases below, at, and above that minimum, plus whitespace-only input.

Then show how the rule interacts with an invalid lifecycle state. Decide whether the caller receives a state error or reason error first. Your answer should not rely on whichever check you happened to write first; explain why that order belongs to the command contract.

Finally, preserve the successful current workflow: a valid manager can reject a Submitted expense, the owner can edit and resubmit, and history remains coherent. An invariant extension that accidentally removes resubmission is a broader behavior change and needs separate justification.

## Independent exercises

Exercise NR16-A: write the protected-field inventory and choose whether policy refresh is allowed on rejection. Give one counterexample that distinguishes your contract from the alternative.

Exercise NR16-B: trace preparation at version seven, a competing update, and stale application. Specify the exact unchanged-state oracle and the successful version-eight event.

Exercise NR16-C: design the normalized-reason extension, its boundary table, and compatibility checks. Explain how encapsulation and collection ownership support the contract without claiming they already exist.
