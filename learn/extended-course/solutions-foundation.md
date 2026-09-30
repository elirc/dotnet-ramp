# Foundation solutions and review notes

Read this guide after writing your predictions. The answers describe the inspected source and the bounded lab. Proposed service tests and reliability changes remain proposals until independently implemented and executed.

## NR01-A: graph and system decisions

The six legal graph edges are Draft to Submitted, Rejected to Submitted, Submitted to Approved, Submitted to Rejected, Approved to Reimbursing, and Reimbursing to Reimbursed. Employee is the human role for the first two, Manager for the next two, and Finance for the fifth. No human role is listed for the final edge. CanSystemTransition returns true for all six because it checks edge existence rather than role-array emptiness.

Rejected has a resubmission edge and is editable under Expense.IsEditable. Reimbursed is the only status classified terminal by IsTerminal. The reimbursement worker still cannot jump from Draft to Reimbursed because that particular edge does not exist. Its destination choice matters independently of the breadth of the system predicate.

## NR01-B: visibility and authority

The owner Employee can view their own expense and pass GetOwned. Another Employee cannot view it through the selected visibility policy. A Manager can view their own and direct reports' expenses; an unrelated Manager cannot view Erin's expense unless another visibility condition applies. Finance can view all selected expenses.

Update uses ownership and editable-state checks, so visibility is not enough. Approve requires both the Manager role and direct-report relationship before its lifecycle transition. A Finance actor viewing a Submitted expense fails the manager relationship guard for approval. An owner whose role is Manager can pass the ownership lookup for Submit but fail the Employee-only transition, assuming policy does not reject first. State those fixture assumptions so the expected first failure is unambiguous.

## NR01-C: enforced versus intended invariants

The shared table is consulted by the inspected service transition helper and background completion path. Public status setters mean the type does not force every possible writer through that helper. The public settable history list likewise does not enforce append-only storage. A stronger proposal might place status changes and event creation behind an aggregate method, but it must address existing fixture construction and serialization rather than assume encapsulation is free.

A good answer preserves the value of the shared table while narrowing the claim. It does not dismiss the implementation merely because comments describe a stronger intention. The useful review question is which writers exist, which checks they use, and which stronger guarantee the product actually needs.

## NR02-A: failed rejection

With an Approved expense and its valid manager, visibility and relationship checks pass. Reject assigns the trimmed new reason before Transition checks Approved to Rejected. That edge is absent, so the transition throws before its status assignment, timestamp update, and history append. The source prediction is a changed reason with the old status and no new transition event; execution of a service fixture would establish the actual object snapshot.

A repair criterion should compare the old and new reason, status, timestamp, history contents, and any other fields the operation is allowed to touch. Validating the edge before assigning the reason is one local proposal. Catching the exception without restoration does not satisfy the criterion. Do not claim a transaction exists around this in-memory object mutation.

## NR02-B: receipt thresholds

With valid description and date, amount exactly twenty-five and no receipt does not trigger RECEIPT_REQUIRED. A larger amount without a receipt does. An attached pending receipt reaches the processing-warning branch; an attached processed receipt does not receive that warning. Other independent warnings may still apply when category or high-value thresholds are exceeded.

Exactly ninety days old is not older than the cutoff. A date beyond that boundary receives the stale warning, while a future date receives the future blocking flag under the selected conditional sequence. Since the engine reads today's UTC date internally, a robust date-boundary test needs a controlled seam or careful execution-bound fixture. The lifecycle lab contains no policy assertions.

## NR02-C: queue events

A useful gate fake records EnqueueAsync invocation and returns a pending awaitable until released. Call StartReimbursement and observe whether it returns while that awaitable remains incomplete. Keep transition state, queue invocation, and queue completion as distinct observations. An arbitrary sleep cannot prove ordering as reliably because it depends on scheduling and machine load.

The worker's loop catches job exceptions and continues processing future dequeues. The selected code does not put the failed delegate back on the queue or create a durable retry record. Therefore, survival and retry are different claims. Simulated payment completion assigns a local reference and history event; it is not evidence of an external financial transfer.

## NR03-A: finite oracle

Six times six times three yields one hundred eight human decisions. Six times six yields thirty-six system decisions. Six next-state assertions and six terminal assertions bring the total to one hundred fifty-six. Reading the private transition dictionary to generate expected values would repeat the implementation's policy and could preserve the same defect on both sides of the comparison.

Ownership, manager relationships, policy refresh, rejection-reason ordering, and enqueue observation are all outside this oracle. Name at least one explicitly in an evidence report. Exhaustiveness over a finite table is valuable, but the word exhaustive must name its domain.

## NR03-B: mutations

Changing the approval role to Finance leaves syntactically valid source and should fail a human-transition comparison. Narrowing the system helper to empty-role edges also compiles and should fail the system decision for a legal human-triggerable edge such as Draft to Submitted. Both are semantic disagreements after a successful build.

A compile failure would show that the mutation or environment is unsuitable for the intended experiment. It would not show that the oracle detects an incorrect policy. The narrower helper could be a deliberate future policy, but adopting it requires changing the contract and reviewing callers rather than presenting it as behavior-preserving cleanup.

## NR03-C: service fixture

Use distinct user identifiers for owner and manager and set the owner's manager relationship to that manager. Put an Approved expense in the store with an existing reason, known timestamp, and known history contents. Snapshot scalar values and copy list contents before calling Reject with a different reason. Catch the expected domain exception and compare every chosen field afterward.

The current characterization should identify the changed reason. A future repaired-contract test would require it unchanged as well. A reference to the original mutable object is not a snapshot, and a reference to its history list may observe later mutation. Make the comparison independent enough to expose the change.

## Capstone review guidance

A strong packet separates three layers: finite lifecycle decisions proven by the copied-source lab, service-order predictions justified by source, and future experiments or repairs. It chooses a small first change, such as validating rejection before assigning the reason, and describes its successful-path compatibility. Another defensible choice is making queue acceptance observable, provided the design does not overstate durability.

The rubric rewards explicit limits because they make the proposal reviewable. A perfect-looking diagram that silently claims a durable payment workflow from an in-memory channel is weaker than a modest design with a clear recovery gap and a reproducible next experiment. Use counterexamples to improve the contract, then decide what evidence is required before implementation.
