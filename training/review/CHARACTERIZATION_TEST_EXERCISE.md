# Characterization test — `ExpenseStateMachine`

This repo has zero test files (see review finding #12). Before anyone is
allowed to touch `backend/Domain/ExpenseStateMachine.cs` — the class whose own
doc comment says "every status change must go through here so that illegal
transitions are impossible" — it needs a test that pins down *exactly* what it
currently allows, so any future change (a real feature, or an accidental
regression like the one in `training/incidents/001-fast-track-reimbursement.md`)
shows up as a visible diff in test output instead of silently shipping.

This is a **characterization test**, not a correctness test: you are not
deciding what the state machine *should* allow from first principles — you are
recording what it *does* allow today, against the class's own documented
diagram, so the current, intended behavior becomes machine-checked.

## Setup

- No test project exists yet. Create one: `backend/ExpenseManagement.Api.Tests`
  (xUnit is the natural choice — it's what the `docs/upskill/05-quality-
  engineering/02-writing-tests-here.md` doc describes, per the audit's read of
  the curriculum tree, though verify that file's actual guidance yourself
  rather than trusting this description). Reference `ExpenseManagement.Api
  .csproj` from the test project.
- You only need `ExpenseStateMachine` itself — it's a static class with no
  dependencies (no `DataStore`, no DI), so this should be the cheapest possible
  test to write in this codebase. If you find yourself needing to stand up
  `WebApplicationFactory` or seed `DataStore` for this exercise, you've picked
  the wrong entry point.

## What to characterize

`ExpenseStateMachine` exposes exactly four members. Write tests that record the
current behavior of each, against the *3 roles × 6 statuses* space:

1. **`CanTransition(from, to, role)`** — for every `(from, to)` pair in
   `ExpenseStatus` × `ExpenseStatus` (36 combinations) and every `role` in
   `UserRole` (3 roles) — **108 cases total** — record whether it currently
   returns `true` or `false`. You don't have to write 108 individual asserts:
   a data-driven test (`[Theory]` + `[MemberData]`/`[InlineData]`, or a single
   test that asserts the *complete* legal set as one collection) is the right
   shape here. The point is that the test's assertion is exhaustive over the
   input space, not spot-checked.
2. **`CanSystemTransition(from, to)`** — for every `(from, to)` pair (36
   combinations), record current behavior.
3. **`IsTerminal(status)`** — for every `ExpenseStatus` value (6 cases).
4. **`NextStates(from)`** — for every `ExpenseStatus` value, record the exact
   set returned.

## Constraints

- Do not consult `training/_answers/` for this exercise — reconstruct the
  legal-transition table by reading `ExpenseStateMachine.cs` and its class doc
  comment yourself; that's the point of a characterization test (you're
  recording *actual* behavior, and "actual" has to come from you reading the
  real code, not from a summary of it).
- The test must fail if `training/incidents/001-fast-track-reimbursement`'s
  change (the erroneous `Submitted → Reimbursing` edge for `Finance`) were
  merged onto `main`. Prove this to yourself: after writing your test, apply
  that incident branch's diff locally (don't merge it — just check out the file)
  and confirm your test goes red. Then revert.
- Keep it to one test file. This is meant to be small and fast, not a
  framework.

## Definition of done

- A new xUnit test project builds and its tests pass against `main` as-is.
- The test suite is exhaustive over the `(from, to, role)` space for
  `CanTransition`, not a handful of examples.
- You've confirmed (per the constraint above) that the suite fails against the
  incident 001 diff, then passes again once reverted.
- `dotnet test` output pasted into your own notes, plus a one-paragraph note on
  what you'd add next if this state machine grew a 7th status.

## Reference

A worked reference implementation and the full expected legal-transition table
are sealed in `training/_answers/characterization-test.md`. Don't open it until
your own suite passes.
