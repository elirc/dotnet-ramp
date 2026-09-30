# 23. Independent capstone: command integrity and client recovery

This capstone integrates the course without requiring changes to the application. Produce a design packet, a bounded experiment, and a reviewable acceptance plan for one chosen command. You may work in disposable educational files, but preserve the original application, dependencies, configuration, and old learning material.

Choose either rejection integrity or reimbursement acceptance as the primary command. The rejection route emphasizes all-fields-unchanged failure and actor relationships. The reimbursement route emphasizes acceptance, background completion, and unknown outcomes. Both require a client recovery design based on the current detail page rather than an invented frontend architecture.

## Deliverable one: current behavior dossier

Write a one-page source map naming the controller, service method, domain helper, response mapping, client wrapper, and detail-page action path involved. Include exact local file links. Mark which parts were executed by a course lab and which remain source-derived predictions.

For rejection, include the reason-before-transition ordering and the manager relationship boundary. For reimbursement, include the unawaited queue call and the distinction between queued work and simulated completion. Do not copy a generic architecture diagram; every arrow should correspond to a selected function call or a proposed new boundary clearly labeled as such.

Add a synthetic actor and expense fixture with explicit identities, role, manager relationship, status, amount, receipt state, timestamp, and history. Explain why earlier guards pass so the target boundary is reached. A fixture that fails before the interesting step cannot support your intended conclusion.

## Deliverable two: an independent contract

State the proposed successful and rejected outcomes in terms of observable fields. For rejection, define whether PolicyFlags can refresh on failure and which fields must remain unchanged. For reimbursement, define the acceptance point and whether a successful response promises in-memory or durable work.

Include an unknown-response case. The command may have succeeded on the server while the client saw a network or parsing failure. Define what the client should display and how it can reconcile before retrying. A contract that only covers clean success and clean rejection is incomplete for an asynchronous application.

If you introduce command IDs or versions, specify their scope, generation, reuse, and mismatch behavior. These are proposed fields, so show how old clients or existing data would be handled. Do not assume adding an identifier automatically implements idempotency.

## Deliverable three: a bounded executable experiment

Use an existing course runner as a starting point or create a separate disposable model with an explicit scope. For rejection, a source-bound service test can compare before-and-after snapshots and a copied repair candidate. For reimbursement, a controlled queue can expose pending acceptance, while a separate model can explore durable recovery assumptions.

Your report must distinguish actual copied application code, controlled collaborators, and any stubbed behavior. The current service lab stubs only the background job factory for its handoff boundary. If you add more stubs, explain what claims they remove from the experiment.

Include one semantic mutation that compiles and violates your contract. For rejection, move an assignment before validation or remove a relationship guard in the copied candidate. For reimbursement, return success before the chosen acceptance boundary or append completion twice on replay. The mutation should fail a specific independent assertion, not merely crash due to invalid syntax.

## Deliverable four: client state transitions

Design client states for idle, submitting, rejected, accepted-but-refreshing, accepted-with-refresh-failure, unknown outcome, and completed where applicable. You may combine states only if their permitted next actions are genuinely identical under your contract.

For each state, specify visible message, displayed expense snapshot, enabled actions, and recovery action. The current busy boolean and error string provide a baseline, but your proposal may need a richer state model. Explain how late responses are tied to the active expense and session generation so older work cannot overwrite newer context.

Include deletion or navigation only if it belongs to your chosen flow. Do not broaden the capstone into a complete application rewrite. The strongest packet improves one end-to-end contract with enough detail that implementation can remain small and reviewable.

## Deliverable five: failure and compatibility matrix

Cover unauthorized actor, invalid lifecycle state, stale version if proposed, queue or collaborator failure, command success followed by refresh failure, malformed successful response, and a late response after route change. For each, state which layer detects the condition and which state remains authoritative.

Compatibility cases should include an ordinary successful command, unchanged response fields used by current pages, and existing history presentation. If you change AvailableActions semantics, explain whether the client still treats them as hints or stronger eligibility information. Keep server authorization independent of the hint design.

For a durable proposal, add crash before commit, after commit before response, and after external effect before local completion. For a local-only proposal, explicitly retain restart limitations rather than claiming the capstone solves them through UI changes.

## Review exercise: change one assumption

At review time, another learner changes one assumption. They may make the manager also own the expense, replace a pending receipt before processing, delay the refresh after successful command, or remove the synthetic provider's idempotency guarantee. Recompute the expected result without rewriting the entire packet.

This challenge tests whether the design is based on causal reasoning or memorized steps. A good packet names the assumption that changed and identifies exactly which guarantee no longer follows. It can retain valid parts of the evidence while narrowing the conclusion.

## Rubric

Award twenty points for the current behavior dossier: eight for accurate call order, six for fixture reachability, and six for evidence boundaries. Award twenty for the proposed contract: eight for successful and rejected field semantics, six for unknown outcomes, and six for identity or version rules.

Award twenty for the experiment: eight for meaningful execution, six for an independent oracle, and six for a compiled semantic mutation with the intended failure. Award twenty for client recovery: eight for truthful state messages, six for safe next actions, and six for late-response ownership.

Award twenty for failure and compatibility reasoning: eight for the matrix, six for preserving ordinary behavior, and six for responding coherently to the changed-assumption challenge. A packet claiming a real payment or full browser integration ran when only a local model executed cannot earn the evidence category until corrected.

A passing packet earns seventy points and contains no unresolved contradiction between its proposed acceptance point and client message. A distinction packet earns ninety and lets a reviewer implement the smallest next change without asking what should happen after an uncertain response.

## Independent exercises

Exercise NR23-A: choose the command and produce the current behavior dossier with a fixture that reaches the target boundary. Identify the smallest meaningful experiment.

Exercise NR23-B: write the command and client-state contracts for success, rejection, and unknown outcome. Include a late response from an older route or session context.

Exercise NR23-C: perform the changed-assumption review and score the packet with the rubric. Record one concrete revision that improves the contract without expanding into unrelated application work.
