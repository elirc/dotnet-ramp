# 04. Independent capstone: an auditable reimbursement decision

The foundation capstone asks you to prepare a reviewable design and evidence packet for one expense workflow. You are not required to modify the application. Your deliverable must distinguish observed current behavior, controlled lab results, and proposed stronger contracts. This distinction is part of the assessment, because an attractive design is not evidence that the current code already implements it.

Choose an employee Erin, direct manager Morgan, unrelated manager Riley, and Finance actor Finley. Give the expense a stable identifier and record its initial owner, amount, date, receipt state, lifecycle status, rejection reason, policy flags, timestamp, history, and payment reference. Use synthetic identities and values. Do not use real employee or payment information in the exercise.

## Stage one: establish the decision boundaries

Create an authority table for viewing, editing, submitting, approving, rejecting, and starting reimbursement. Every allowed or denied entry must cite a source boundary: ownership lookup, visibility lookup, direct-report check, policy blocking check, or lifecycle role-and-edge check. Where several checks apply, write their order.

The strongest submission explains a visible-but-forbidden action. Finance visibility does not make Finance a Manager for approval. A manager's visibility of their own expense does not automatically establish that they manage its owner under the selected relationship check. These cases force the table to include identities and relationships rather than only role names.

Also identify what this table assumes about the supplied User object. A service-level analysis does not prove a real HTTP request cannot forge or misbind identity. You may propose an endpoint test, but label it as additional evidence rather than silently treating it as completed.

## Stage two: characterize one failed action

Use the Approved-to-Rejected path with a valid manager and a different reason. Write a before-and-after prediction for every field affected by Reject or Transition. Explain why reason assignment can precede the invalid-edge exception while status and transition history remain unchanged under the inspected sequence.

Then state a proposed all-fields-unchanged rejection contract. Define the comparison carefully: values, collection contents, and timestamps matter, while object-reference identity might or might not be part of your chosen requirement. If you include policy flags, explain whether validation is allowed to refresh them on a failed operation. A vague promise of no changes is insufficient without a field scope.

You may sketch a repair that computes proposed values and validates before assignment. Discuss how the repaired operation would preserve successful behavior, including reason trimming and event content. Do not implement unrelated authentication, storage, or user-interface changes as a shortcut around the small invariant.

## Stage three: separate handoff from completion

Draw an ordered trace containing transition to Reimbursing, enqueue invocation, enqueue completion, worker dequeue, system-edge validation, and simulated payment completion. Add a queue-full interval and a cancellation-during-delay interval. Use separate traces if one ordering cannot represent both scenarios clearly.

For each interval, state what the current source establishes and what needs execution. The unawaited ValueTask establishes an observation gap in the service method. It does not establish that every call fails. The worker's catch-and-continue behavior establishes survival after a job exception in the loop; it does not create a retry policy for that job.

Your proposed reliability contract should choose an observable outcome for accepted work, rejected work, and work interrupted after acceptance. If you propose durable storage, explain the relationship between expense status and work-record commit. If you propose only a better in-memory handoff, explicitly retain the process-restart limitation.

## Stage four: present bounded evidence

Run the copied-source lifecycle lab and include its baseline assertion count, two mutation outcomes, and original-source hash result. Explain which part of your capstone this evidence supports. It supports lifecycle-table predictions; it does not execute the rejection or queue traces.

For the unexecuted parts, supply a precise test plan using controlled collaborators. Include the first assertion that would fail under a plausible defect. A useful plan names inputs, setup, blocking gates, observations, and expected outcomes. A list of test names without those details is not enough to reproduce your reasoning.

Finish with a decision note describing the smallest proposed improvement you would implement first and why. The reason should follow from a concrete contract failure or uncertainty, not from a preference for a fashionable architecture. A reviewer should be able to approve, reject, or request a narrower experiment from your packet.

## Assessment rubric

Award twenty points for the authority model: ten for correct role-and-edge decisions, five for ownership and manager relationships, and five for ordered rejection boundaries. Award twenty for failure-state reasoning: ten for the current partial mutation, five for a precise unchanged-state proposal, and five for successful-path compatibility.

Award twenty points for handoff and worker reasoning: eight for distinct events, six for queue and cancellation uncertainty, and six for separating worker survival from retry or durability. Award twenty for evidence: ten for correct interpretation of the actual-source lab, five for useful service-fixture design, and five for honest limitations.

The remaining twenty points assess design communication: ten for a coherent minimal proposal, five for explicit current-versus-proposed labels, and five for reproducible synthetic examples. A submission that claims a full application or payment integration was tested when only the table lab ran cannot earn the evidence or communication points until that claim is corrected.

A passing foundation packet earns at least seventy points and contains no unresolved identity or evidence misrepresentation. A distinction-level packet earns ninety and makes its strongest counterexample easy for another learner to reproduce. The separate solutions provide a model reasoning path, not a single required architecture.
