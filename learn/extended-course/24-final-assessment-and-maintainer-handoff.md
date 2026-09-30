# 24. Final assessment and maintainer handoff

This assessment checks whether you can transfer the course's reasoning to unfamiliar combinations of the existing expense workflow. It does not introduce new application features. Use the source links in earlier chapters, the four bounded runners, and the separate review guides. Write your answers before opening the final solutions.

## Station one: classify evidence precisely

You receive four statements: every lifecycle combination was tested, receipt files are valid images, reimbursement work is durable, and the client handles every server error as ApiError. Decide which statements follow from the executed labs, which contradict the selected source scope, and which need additional evidence.

The lifecycle lab enumerates defined statuses and roles for the table, but does not test every service or HTTP path. ReceiptStorage checks metadata rather than decoding image content. The queue is in-memory, and the service does not observe reimbursement acceptance. The wrapper parses nonempty text before creating ApiError, so non-JSON bodies can fail earlier.

Your answer should rewrite each statement into a defensible claim rather than merely label it true or false. This is an important maintenance skill: precise narrower claims remain useful, while vague confidence language makes future changes harder to review.

## Station two: reach the intended guard

Design a fixture proving the direct-report approval guard matters. Then explain why an unrelated manager fixture might fail too early to test that guard. Next design a fixture proving the actual actor role matters for submission even when ownership passes.

The evaluator should remove one guard in a copied source variant and ask whether your test fails. If another earlier guard still rejects, revise the fixture until the target condition is isolated. This station assesses test sensitivity, not merely branch coverage.

Add an all-fields snapshot for one rejected command. State whether equal-content list replacement counts as change under your contract. A test cannot prove an unspecified notion of unchanged state.

## Station three: distinguish three clocks

Explain the difference between ExpenseDate, an event timestamp, and a worker delay. Design one deterministic boundary test for policy age, one for session expiry, and one controlled scheduling test for queue capacity. State why sleeping is unnecessary for the first two and insufficient as the sole synchronization method for the third.

Then identify the module-initialized default date in the form and the local display conversion in the date helper. These are client-side time semantics that a pure PolicyEngine test does not cover. A complete answer keeps the layers connected without claiming one test proves them all.

## Station four: recover from an uncertain command

A user clicks reimburse. The server changes status, but the response body is malformed and the client throws a parsing error. The next detail refresh also fails. Write the client message and safe next action under your proposed contract.

Do not assert that reimbursement failed merely because the client could not parse the response. Also do not assert that payment completed merely because the server may have accepted the request. Reconciliation should query authoritative state or command identity when available, with the current implementation's limits clearly stated.

If your proposal adds durable command identity, explain what happens when the same key is reused with a different expense. If it remains local-only, explain how the user can recover without a guarantee of durable work. The evaluator should be able to see exactly which uncertainty your design resolves and which it retains.

## Station five: preserve report meaning during optimization

A maintainer proposes paginating ExpenseService.List and leaving ReportingService unchanged. Identify the risk to aggregate completeness. Then propose a separate aggregation contract or query path that preserves actor visibility, inclusive date filters, status definitions, and currency meaning.

Add a dataset larger than one page and include an out-of-scope expense with a distinctive amount. The expected report should fail if it aggregates only the first page or broadens visibility. Include equal UpdatedAt values so pagination's ordering contract is tested independently.

Finally, explain why default USD formatting cannot repair mixed-currency arithmetic. The server data contract and client display must agree on units. A faster report with misleading units is not a successful optimization.

## Station six: investigate without overcorrecting

A receipt remains Pending after a replacement upload. Write the smallest evidence collection plan that distinguishes old-job lookup, new receipt processing, queue acceptance, and stopped browser polling. Choose synthetic reproduction data and avoid reading real receipt contents.

Propose a recovery only after identifying a cause. A stale browser view may need a refresh; a known failed current job may need processing retry; an orphan file may need cleanup. Reuploading or restarting everything can obscure evidence and create additional state, so the packet should justify any broader action.

The evaluator changes the scenario so the old job had already obtained a reference before metadata replacement. Explain which parts of your original conclusion still hold and which need a new interleaving experiment.

## Scoring and independence

Score each station out of fifteen: five for correct source reasoning, five for a concrete independent fixture or trace, and five for evidence limits and recovery semantics. The six stations total ninety points. Add ten points for a maintainer handoff that another learner can use without reading the entire conversation that produced the course.

A passing result earns seventy points and does not misrepresent a model as application execution. A distinction result earns ninety and includes at least one counterexample that exposes a plausible but incorrect implementation. If an answer depends on a source change, update the evidence map and expectations explicitly.

## The maintainer handoff

Write a concise change proposal naming the concrete problem, resulting behavior, affected boundaries, and validation. For example, a rejection-atomicity proposal should state that invalid transitions no longer change the reason and identify the before-and-after snapshot test. It should not bury that behavior under a list of helper names.

Record the exact runner commands you executed and their scope. The lifecycle, queue, and service runners compile copied selected C# source in disposable projects. The client runner imports actual JavaScript with fake globals. None of them starts the full application, executes a real browser, or transfers money.

List source hashes or revision identity where available, current assumptions, and remaining tests needed before deployment. Do not include credential values, private receipt contents, or invented benchmark numbers. The handoff should let the next maintainer repeat the evidence and decide what additional integration work is necessary.

## Keep the course maintainable

When source changes, review linked claims and lab mutation anchors. A failed anchor can mean a legitimate refactor rather than a product defect. Reestablish the experiment's scope before changing expectations. Likewise, a newly failing characterization may reflect an intentional repair that needs an updated current-behavior explanation.

Do not preserve stale flaw claims merely because they make a dramatic exercise. Historical incidents belong in clearly labeled historical material. Current-source modules should teach the code that exists, and proposed designs should remain labeled until implemented and tested.

The route is complete when you can explain the command, state, resource, and evidence boundaries without relying on a single success flag. The assessment rewards that precision because it supports safe small changes and honest collaboration on a real codebase.

## Independent exercises

Exercise NR24-A: complete stations one and two and rewrite every overbroad claim into a defensible statement. Include the guard-removal counterexample.

Exercise NR24-B: complete stations three and four with deterministic time and unknown-outcome traces. State exactly what the client can know at each step.

Exercise NR24-C: complete stations five and six and write the maintainer handoff. Score your work and identify the smallest unresolved integration test before any implementation is published.
