# Design and assessment solutions: chapters 16-24

These answers are review models rather than one mandatory architecture. Award ten points per exercise: four for correct current-source or declared-design reasoning, three for a concrete trace or fixture, and three for explicit evidence boundaries and compatibility. The twenty-seven exercises total two hundred seventy points. The capstone and final assessment also have their own rubrics for integrated work.

## NR16-A: protected fields

A strict unchanged-state contract protects all expense values, including reason, policy flags, status, timestamps, receipt reference, and history contents. A narrower contract can allow derived policy refresh while protecting command and lifecycle state. The current invalid rejection violates the strict contract by changing reason; a blocked Submit can replace policy flags while leaving lifecycle unchanged.

The counterexample distinguishing the two proposed contracts is a failed submission whose recomputed flags differ from the old list. The narrower contract permits that update; the strict one does not. State whether reference replacement with equal contents also matters, because semantic equality and object identity are different requirements.

A good design inventory names fields even when they are not expected to change. That lets a test detect accidental payment-reference or receipt-pointer mutation during an unrelated rejection repair. The purpose is a bounded aggregate contract, not indiscriminate comparison of unrelated global state.

## NR16-B: stale prepared change

Prepare a rejection from Submitted at version seven, capturing normalized reason and one event proposal. If another accepted command advances the expense to version eight before application, the prepared change must fail its expected-version condition and leave all protected fields at the newer accepted state. It must not restore the old version-seven snapshot, because that would overwrite legitimate concurrent work.

Without contention, applying the proposal advances to version eight, changes status to Rejected, assigns the normalized reason, updates the timestamp according to the chosen captured-time contract, and appends one matching transition event. Unrelated amount, owner, and receipt values remain stable.

This answer assumes a proposed atomic version check and application boundary. The current Expense has no version property. A sequential preparation function alone does not establish concurrency safety, so the design must name the lock, conditional write, or immutable compare mechanism that makes acceptance authoritative.

## NR16-C: normalized reason extension

Choose a proposed minimum explanation length and test one below, exactly at, and one above after trimming, plus whitespace-only input. For example, a minimum of five characters would reject four normalized characters and accept five, but the number is an exercise requirement rather than current policy. Preserve the actual normalized string on successful rejection.

Decide whether authorization, lifecycle, or input validation errors take precedence when several conditions fail. A defensible order resolves authority before revealing object-specific state, then validates the command under the chosen API contract. The exact choice must be consistent and tested.

Encapsulation can route protected changes through one aggregate method, while copied or immutable collections prevent shared-list mutation from bypassing it. Compatibility tests retain valid manager rejection, owner editing of Rejected, and resubmission. A stricter reason rule should not accidentally make Rejected terminal or remove existing successful transitions.

## NR17-A: old and new receipt identities

After R2 replaces R1, the old job still carries R1's identifier. If R1 metadata has been removed before its lookup, the bare processing job returns without marking R2 processed. The wrapper then looks up E and refreshes policy using E's current ReceiptId, which points to R2.

Therefore processing identity and policy identity differ. The expected policy follows R2's actual status; a Pending R2 remains a processing-warning case. Checking only the final flags would not prove which receipt object was processed, so record both metadata statuses and the captured job identifier.

If a different schedule lets a job retain an object reference before removal, the analysis must use a reachable scheduling boundary. The current delay occurs before lookup. Do not invent an await between arbitrary statements and call the resulting model an unmodified-source execution.

## NR17-B: competing replacements

One coherent proposal uses an expected expense version so only one concurrent replacement attaches to the version both callers observed. The winning attachment defines the linearization point. The losing request reports conflict and schedules cleanup of its newly stored but unattached resource, subject to retention requirements.

A controlled schedule lets both file copies finish, applies R2 first, then attempts R3 with the stale version. The oracle inspects E's pointer, both metadata records, both file paths, queued jobs, and cleanup state. It should not assume the losing file disappears automatically.

The current code has no such version protocol. A last-assignment-wins proposal is also possible, but then user messaging and orphan cleanup must explain that accepted uploads can be superseded. The design should choose one contract rather than switching between them depending on which test is easiest.

## NR17-C: attempt and cleanup states

Useful proposed states include copying, stored-unattached, attached-pending, processing, processed, superseded, cleanup-pending, and needs-review. Transitions need owners and retry rules. A failed cleanup remains visible as cleanup-pending or needs-review rather than disappearing behind a completed upload response.

Cleanup should verify that the resource is still unreferenced and stay within its owned storage root. If audit retention forbids deletion, the compensating action can mark the resource unlinked instead. Compensation is another fallible operation, not a transaction rollback across disk and database.

For policy lag, record receipt processed time and expense policy-refresh time separately. A proposal can make the update atomic, compute policy on read, or expose eventual consistency. The current polling condition and mutable store motivate the experiment but do not prove a live stale-policy incident.

## NR18-A: acceptance point

The proposed successful request commits both Reimbursing state and a durable work record in one local transaction, along with coherent history. Current service invocation does less: it changes state and calls enqueue without observing completion. Awaited in-memory acceptance would be stronger than invocation but still would not survive process loss.

The response should describe accepted work, not completed payment. Include a stable work or command identity if the client must reconcile an unknown response. Define replay scope and payload mismatch behavior so the identity does more than decorate the response.

The transaction boundary must cover all local facts the response promises. If the work insert can fail after the expense update commits, the proposed guarantee is not met. A fault-injection test should fail between those writes and require either both absent or a declared recoverable state.

## NR18-B: crash matrix

Before local commit, retry can create the first accepted command. After commit before response, retry should discover the existing accepted identity. After claim, recovery must handle an abandoned lease or claim. After provider success before local completion, the external effect is uncertain locally and must be reconciled. After completion commit, replay should return the completed outcome without another event.

Safe provider replay requires an explicit assumption such as stable operation identity with repeat calls returning the same effect. Local database atomicity alone cannot guarantee one external effect. If that assumption is removed, the model should expose an unknown-outcome or manual-reconciliation state rather than retain an unsupported exactly-once claim.

A good answer records both local durable state and provider state at every crash point. Looking only at the expense status hides the most difficult window, where the provider may have acted while local completion remains absent.

## NR18-C: claim and completion protocol

A claim should be acquired through an atomic condition and carry an owner or generation. A stale owner must not overwrite a newer claim's result. Retryable, permanent, and uncertain failures need distinct states and escalation rules. Completed replay should be recognized by stable work identity.

The completion oracle checks one business completion event, one authoritative payment reference under the synthetic provider contract, and a completed work record linked to the same expense. Replaying completion must not append a duplicate event or silently replace another operation's reference.

A lease expiry does not prove the old worker stopped. Use a fencing or duplicate-safe effect strategy where required. The current in-memory worker has no durable claim record, so these are proposed design requirements and model tests, not properties established by the existing queue lab.

## NR19-A: time categories

ExpenseDate and policy today are calendar dates. CreatedAt, UpdatedAt, event times, and session expiry are instants. Worker delays and retry waits are elapsed durations. A date policy can accept an explicit DateOnly reference day, a session service can use an injected UTC instant provider, and a scheduler can expose controllable delay completion.

These seams need not share one oversized interface. The important property is that each dependency exposes the concept the consumer actually needs. A fake wall clock does not automatically control Task.Delay, and advancing an elapsed timer does not decide which business timezone defines today.

The tests should make those distinctions visible. A date-boundary assertion uses exact calendar values, an expiry assertion compares instants, and a queue-capacity test uses a state gate rather than elapsed time as its primary oracle. This keeps deterministic testing aligned with domain meaning.

## NR19-B: fixture topology and ties

Assign distinct nonempty identities to owner, manager, expense, receipt, and proposed work. Reserve Guid.Empty for the system actor meaning already used by the source. Check that the owner's ManagerId points to the intended manager and that receipt metadata references the intended expense.

Give two expenses equal UpdatedAt values and require an explicit secondary order in a pagination proposal. The current descending timestamp expression alone does not specify a business tie-breaker. A deterministic identifier order is one possible extension, but its direction and cursor encoding must be stated.

A fixture factory should fail early on accidental identity collisions rather than silently overwrite dictionary entries. Avoid a universal fixture that makes every actor an owner and manager; it can cause authorization tests to pass through relationships the learner did not intend to exercise.

## NR19-C: deterministic boundaries

For policy, supply a fixed today and test tomorrow, today, exactly ninety days earlier, and ninety-one days earlier. For expiry, test immediately before E, exactly E, and immediately after E under the greater-than-or-equal rule. For transition timing, use a sequence clock to reveal whether timestamp and event time are read separately or captured once.

The current service lab chooses yesterday to avoid date boundaries while testing unrelated behavior. That is appropriate fixture isolation, but it does not establish exact cutoff semantics. The current implementation still reads time internally, so the exact tests require a proposed seam or another clearly documented controlled technique.

A useful report states which clock was controlled and which remained real. Claiming deterministic time merely because one input date was fixed is too broad if the engine still reads the machine clock independently.

## NR20-A: shared-reference assumptions

Create inserts a new object directly. Update and transition methods mutate retrieved references. Submit replaces a list on the stored object. Reject can mutate before throwing. Receipt replacement spans dictionaries, an expense reference, local files, and queued work. Reporting enumerates mutable values and reads their fields across several passes.

A detached database value would not persist those property assignments without an explicit save. A transaction boundary would also need to be defined for status and history together. Replacing a dictionary with a repository name does not automatically preserve shared-reference semantics.

The migration inventory should distinguish commands, queries, external resources, and derived state. That lets the design choose explicit persistence operations rather than create a broad adapter that imitates mutable dictionaries while hiding when writes actually occur.

## NR20-B: aggregate save

A coherent proposed schema stores expense identity, owner, status, amount and currency, receipt reference, timestamps, and version, with related transition events. An accepted command conditionally updates the expected version and appends its event in one transaction. A stale expected version produces a conflict under the selected API policy.

Authorization should be resolved according to the visibility contract before returning object-specific conflict details. Missing, unauthorized, and stale are separate internal causes, but the external response can intentionally conceal existence where required. State that policy rather than leaking whichever database exception happened first.

Receipt bytes remain outside the relational transaction unless a different storage design is chosen. Their cleanup and attachment protocol therefore need explicit recovery. A foreign key can enforce metadata relationships but cannot remove an orphan file by itself.

## NR20-C: migration and adapter tests

The synthetic migration dataset should include a normal expense, padded currency, a missing owner, a dangling receipt, and inconsistent history. Specify which values normalize, which remain unchanged, and which require review. Preserve original synthetic input so the transformation is auditable and reversible where promised.

Adapter tests should cover visible queries, conditional version save, transition-and-event atomicity, and failure after the status update but before event append. Under an all-or-nothing transaction contract, that injected failure leaves the old status and no new event.

Do not run these proposals against the user's real data or imply a database adapter was installed. The course supplies a migration contract and disposable-test design. Actual provider translation, collation, precision, and transaction behavior require a later integration implementation and execution.

## NR21-A: workload measurements

Use an Employee with a small owned set, a Manager with direct reports, and Finance with broad visibility. Record total stored rows, visible rows, filtered rows, returned rows, serialized bytes, and rendered rows. These counts identify where growth occurs before timing introduces environmental noise.

Server scanning can grow even when the final response is small. Response size affects transport and parsing. Browser row count affects rendering and interaction. A single latency number cannot distinguish these costs or prove the same behavior for all roles.

A representative synthetic dataset should include statuses, categories, dates, and timestamp ties. Record its generation recipe and exclude startup or seeding time unless intentionally measured. Do not invent benchmark numbers to fill a report; a clear measurement plan is more useful than unsupported precision.

## NR21-B: mutable sort keys

After a first page containing E3 and E2, editing E1 can move it before the continuation boundary. Offset pagination can repeat or skip rows as positions shift. A timestamp-and-ID cursor can avoid some insertion shifts but can still omit a row that moves before the cursor after the first page.

Choose a contract such as live forward traversal with refresh for newly moved rows, or a snapshot-bound browsing session. The oracle must follow that choice for insertion, deletion, edits to seen and unseen rows, and equal timestamps. No pagination mechanism automatically provides a stable historical snapshot without additional state.

The current list is unpaginated and sorts only by UpdatedAt. Any cursor encoding and tie-breaker are proposed changes. Preserve ordinary visibility and filter semantics while testing the new continuation behavior independently.

## NR21-C: report completeness and cache scope

A separate aggregation query should cover the complete visible filtered set even if list results are paginated. Use more rows than one page and a distinctive amount on a later page so first-page-only aggregation fails. Include an out-of-scope expense with another distinctive amount to catch visibility broadening.

A proposed cache identity must include actor or visibility scope and relevant filters. Invalidation must account for changes that affect totals or scope, including manager relationships where applicable. Caching by route alone is insufficient when authenticated users receive different data from the same endpoint.

Correctness checks should accompany any performance result: same scoped totals, status definitions, currency semantics, and declared ordering. A faster response that loses later-page expenses or labels mixed units as USD is not a successful optimization.

## NR22-A: changed-reason investigation

The evidence ledger should separate the user's symptom, selected source order, copied-service reproduction, and missing live-request correlation. The lab proves that a valid manager rejecting an Approved expense can change reason before DomainException under the fixture. It does not prove that every real changed reason came from that request rather than another writer.

A live investigation would correlate expense identity, actor, command, starting state, and outcome without logging unnecessary free-form reason text. The smallest next change can validate before assignment and add an unchanged-state test, with explicit compatibility for valid rejection.

A strong conclusion names the local ordering defect and its evidence boundary. It avoids broad claims that all validation or all audit history is broken. Precision makes the proposed repair smaller and easier to review.

## NR22-B: pending receipt decision tree

First identify E's current receipt, then distinguish R1's queued work from R2's current metadata. Check acceptance, job start, metadata lookup, processing result, policy refresh, and client fetch in that order. A stale display and an unprocessed receipt are different causes even if both show Pending to the user.

If the server has current processed data but the browser stopped after a failed poll, retrying the read is the narrow recovery. If current R2 processing failed with a known retryable cause, retry that work. If only an old unreferenced file remains, cleanup is a separate storage task. Reuploading is not a universal first response.

The old-job-held-reference variant needs a reachable interleaving analysis. Preserve conclusions about captured identifiers, but do not assume map removal destroys an object reference already held elsewhere. Record what the source and controlled experiment actually establish.

## NR22-C: lifecycle metrics

Useful counters include accepted commands at the chosen acceptance boundary, queue acceptance completions, job starts, successful job completions, and skipped or failed jobs by distinct category. A pending-writer gauge should reflect writers awaiting capacity, not merely total enqueue calls. Each increment or decrement point belongs in the test plan.

Use request identity for the incoming command and work identity for later execution, with expense and receipt identities where needed. A history note created before observed acceptance is not durable queue evidence. If acceptance was never observed, the honest conclusion is that current evidence cannot distinguish pending or lost handoff from later processing absence.

The next experiment should instrument that missing boundary or use a controlled queue, not claim a root cause from temporal proximity. No metric name should combine policy denial, transport uncertainty, and worker exception without a clear definition.

## NR23-A: capstone dossier

For rejection, the dossier should connect the controller's manager authorization, current-user resolution, service visibility and relationship checks, reason assignment, lifecycle validation, response mapping, wrapper error handling, and detail act path. A Manager-owned fixture can isolate the relationship guard; an Approved direct-report fixture isolates partial reason mutation.

For reimbursement, connect Finance authorization, service transition, queue invocation, worker completion, and detail polling. The smallest experiment may be the controlled queue gate rather than a full host. Name which existing runner supplies evidence and which additional boundary remains unexecuted.

A dossier earns full credit when another learner can reproduce its target path from the stated fixture. Listing filenames without causal order or using an invalid fixture that fails too early is insufficient.

## NR23-B: command and client states

A rejected command should expose its known rejection category and preserve state according to the proposed aggregate contract. Accepted-but-refresh-failed should retain acceptance evidence while warning that the displayed snapshot may be stale. Unknown outcome should trigger reconciliation before automatic repeat. Completion is a separate state for background work.

Late responses should carry or be checked against route and session generation so an old request cannot replace a newer context. Test both late success and late failure. A newer login must not be cleared by an older request's authentication error under the proposed ownership rule.

The contract should explain command IDs or versions if introduced, including reuse with a different payload. A key alone is not replay storage. Keep client messages aligned with the actual acceptance point, especially if the design remains in-memory and cannot promise restart durability.

## NR23-C: changed assumption and scoring

A strong review changes one assumption and recomputes only the affected guarantees. If the synthetic provider loses idempotency support, the at-most-one external-effect claim no longer follows, but local transaction atomicity can remain valid. If the manager becomes the owner without a reporting relationship, visibility can remain true while approval authority changes.

Score the dossier, contract, experiment, client recovery, and compatibility using the capstone's five twenty-point categories. Record a concrete revision, such as adding an unknown-outcome state or an independent relationship fixture, rather than expanding into unrelated features.

The exercise is successful when the proposal becomes more precise under challenge. A packet that simply restates its original conclusion after an assumption changes has not demonstrated causal understanding, even if its diagrams look complete.

## NR24-A: evidence and guards

Rewrite exhaustive lifecycle testing as exhaustive defined-status and role decisions for the copied table. Rewrite receipt validity as selected length, content-type, and extension checks, not decoded content validation. Rewrite reimbursement reliability as observed local transition and queue behavior within the executed boundary, not durability. Rewrite universal ApiError handling to acknowledge parsing and network exceptions.

For guard isolation, use a Manager-owned Submitted expense without a self-report relationship to reach EnsureManagesOwner after visibility passes. The compiled service mutation demonstrates that removing the relationship condition changes the outcome. An unrelated manager may fail earlier and cannot establish the same sensitivity.

Full credit requires these narrower claims to remain useful. Merely saying nothing is proven discards real evidence. The skill is to preserve what the lab establishes while refusing unsupported extensions.

## NR24-B: time and uncertainty

Calendar-date policy, instant expiry, and elapsed queue waiting require distinct controls. Use explicit today for age boundaries, explicit now for expiry equality, and a capacity gate for queue acceptance. Client module-date freshness and localized date-only display remain separate component and formatting concerns.

For an unreadable reimbursement response followed by failed refresh, the client cannot know whether the command was accepted from those failures alone. Show an unconfirmed-outcome state and offer reconciliation. If a durable command identity exists under the proposal, query it; otherwise explain the current system's narrower recovery options.

Do not show payment complete without completion evidence or command failed solely because parsing failed. The correct message follows the known boundary, and the next action should avoid creating duplicate business work under uncertainty.

## NR24-C: optimization and incident handoff

Preserve complete report scope when adding list pagination through a separate aggregation contract. Test later-page amounts, out-of-scope rows, equal timestamps, and mixed currencies. A default currency formatter cannot supply missing conversion semantics.

For pending receipt investigation, record current receipt identity, old job identity, acceptance, processing, and client observation. Choose recovery after establishing the cause. The held-reference variant requires a reachable scheduling model and does not invalidate every earlier identity conclusion.

The maintainer handoff should state the concrete proposed behavior, exact executed commands and scopes, source identity, compatibility cases, and the smallest remaining integration test. It should not include real credentials, receipt contents, or invented timings. Score the six stations and handoff honestly, retaining unresolved evidence gaps as explicit next work rather than disguising them as completed validation.
