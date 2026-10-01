# Refactor & Design Katas

Judgment reps. Each: do the thinking, then self-grade **Basic / Solid / Strong**.

---

### Kata 1 — Fix a boundary leak
Suppose a new controller returns `Expense` directly. Refactor to a DTO without changing the
URL or status codes. **Strong:** you also add a test asserting no sensitive field serializes.

### Kata 2 — Introduce the outbox for one flow
Design (don't fully build) the outbox for reimbursement. Produce the table shape, the
transaction boundary, and the dispatcher loop. **Strong:** you state the exactly-once +
idempotency argument and the failure/dead-letter handling. (See [Project 2](03-senior-build-projects.md).)

### Kata 3 — Split a growing module
`ExpenseService` does CRUD + authorization + transitions + reimbursement. If it doubled in
size, how would you split it (e.g. `ExpenseAuthorizer`, `ExpenseWorkflow`) without scattering
the rules? **Strong:** you keep a single authorization source of truth and justify the seams.

### Kata 4 — Remove duplication between Create and Update
[`Create` L67-84](../../../backend/Services/ExpenseService.cs#L67-L84) and
[`Update` L86-101](../../../backend/Services/ExpenseService.cs#L86-L101) share field-copy logic.
Extract it without obscuring the editable-state guard. **Strong:** you note that
over-DRYing two things that may diverge (create vs update rules) is a trap.

### Kata 5 — Improve type safety of `availableActions`
Today it's `List<string>` ([`Mappings` L45](../../../backend/Services/Mappings.cs#L45)); the
client matches magic strings. Propose a safer contract (enum/const) shared in spirit across
the boundary. **Strong:** you weigh the cost (no shared types across C#/JS) vs. benefit.

### Kata 6 — Design a clock seam
`PolicyEngine` reads `DateTime.UtcNow` ([L37](../../../backend/Services/PolicyEngine.cs#L37)),
making date rules non-deterministic to test. Introduce `TimeProvider`/`Func<DateTime>`.
**Strong:** minimal surface change, defaults preserved, and a date test that no longer
depends on the wall clock.

### Kata 7 — Reduce a (future) N+1
Given the EF Core migration, rewrite `Mappings` list-mapping to avoid per-row user lookups.
**Strong:** you batch-load and explain how you'd *detect* the N+1 (query log) before and after.

### Kata 8 — Write an RFC
Pick any senior project and write the RFC (template in [07/02](../07-career-and-collaboration/02-writing-prs-and-rfcs.md)).
**Strong:** it has alternatives considered, a rollout/rollback, and explicit open questions —
not just a plan you already decided on.

### Kata 9 — Review a flawed PR
Take [Review Kata 1 or 8](../04-code-reading-gym/04-review-katas.md) and write the full review:
findings with severity, the one *blocking* issue stated kindly, and a suggested smaller PR if
the author over-reached. **Strong:** you separate "must fix" from "preference" and propose a
path forward, not just objections.
