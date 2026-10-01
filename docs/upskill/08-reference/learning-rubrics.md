# Learning Rubrics

Observable behaviors, not vibes. Tick what you can *demonstrate* on this repo.

## Junior — "I can make it work"
- [ ] Run backend + frontend and log in as each demo role.
- [ ] Trace a feature (e.g. submit) from button to store using the [key flows](../01-codebase-cartography/05-key-flows.md).
- [ ] Add a category / field end to end following an existing pattern.
- [ ] Read a `ProblemDetails` error and find the responsible line.
- [ ] Explain why responses are DTOs, not entities.
- [ ] Open a small, focused PR with a clear description.

## Mid-level — "I can make the right thing, and prove it"
- [ ] Add an endpoint with correct **resource-scoped** authorization (not just role).
- [ ] Write service-level tests incl. the cross-team IDOR rejection ([Recipe 4](../05-quality-engineering/02-writing-tests-here.md)).
- [ ] Add a state to the state machine without breaking existing edges.
- [ ] Debug systematically: narrow to a layer before editing ([05/03](../05-quality-engineering/03-systematic-debugging.md)).
- [ ] Identify an N+1 / a latent perf issue and explain the fix.
- [ ] Review a PR with severity-tagged, anchored, kind comments.
- [ ] Spot a captive-dependency or boundary-leak risk in a diff.

## Senior — "I can judge what it commits us to"
- [ ] Critique the architecture: name the top 3 risks with evidence ([critique](../03-architecture-and-patterns/06-architecture-critique.md)).
- [ ] Write an RFC with alternatives, migration, and rollback ([Kata 8](../06-contribution-practice/04-refactor-and-design-katas.md)).
- [ ] Design the EF Core migration keeping the API contract stable.
- [ ] Design durable, idempotent reimbursement (outbox + retries + detector).
- [ ] Sequence a multi-project plan (tests → persistence → reliability → observability → scale).
- [ ] Decide *not* to do something and articulate the trade-off.
- [ ] Mentor: turn a vague ticket into acceptance criteria + a test plan.

## The mindset ladder
> Junior: *how do I make it work?* → Mid: *is this the right pattern?* → Senior: *what does
> this commit us to, who pays the cost, and how do we reduce the blast radius?*

Re-read this monthly; the gaps are your study plan.
