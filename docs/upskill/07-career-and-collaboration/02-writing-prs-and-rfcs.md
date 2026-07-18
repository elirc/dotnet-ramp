# Writing PRs & RFCs

## Commit messages

Imperative, scoped, explain *why* when non-obvious:

```
Add Training expense category

Mirrors the existing category pattern (enum + PolicyEngine limit + UI list).
No limit was added intentionally for X reason... (or: limit set to $500 per finance).
```

Avoid "fix stuff", "wip", "address comments" as the final message.

## PR description template (tailored to this repo)

```markdown
## What
<one line>

## Why
<the problem / ticket>

## How
- <key changes; which layer>
- Goes through the service / state machine? (yes/no — if no, why)

## How tested
- [ ] Unit/service test added (which)
- [ ] Manual: <steps, which demo account>
- Note: repo has no CI yet, so reviewers should pull and run.

## Risk & blast radius
- Contract change? (DTO/route) <yes/no>
- Authorization touched? <yes/no — how verified>
- Rollback: <how>

## Follow-ups
- <deferred scope, linked>
```

## When to write an RFC instead of just a PR

Write an RFC when the change: alters a contract (DTO/route), touches the state machine or
auth model, introduces persistence/a dependency, or affects the money path. I.e. all the
[senior projects](../06-contribution-practice/03-senior-build-projects.md). For a one-file
ticket, skip it.

## RFC template

```markdown
# RFC: <title>

## Problem
What's broken/missing and who feels it.

## Goals / Non-goals
Explicit scope boundaries.

## Proposal
The design. Diagrams/anchors to current code.

## Alternatives considered
At least two, with why-not. (A senior signal: you didn't just pick the first idea.)

## Migration & rollout
Steps, dual-running, feature flags.

## Rollback
How to undo safely.

## Testing
What proves it works, incl. failure injection.

## Risks & open questions
Honest unknowns.
```

## Worked example (skeleton)

For "Reliable reimbursement (outbox)":
- **Problem:** stuck `Reimbursing` on crash, no retry ([03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md)).
- **Alternatives:** (a) await + transaction only — still at-most-once; (b) broker — heavy;
  (c) DB outbox + dispatcher — chosen.
- **Rollback:** feature-flag dispatcher; fall back to direct enqueue.
- **Testing:** crash-mid-flight → exactly-once payment.

Filling that page *before* coding is the deliverable. See
[Kata 8](../06-contribution-practice/04-refactor-and-design-katas.md).
