# Answer — Incident 001 (fast-track reimbursement bypass)

## Root cause

`backend/Domain/ExpenseStateMachine.cs`, the `Transitions` table gained a second
entry under `[ExpenseStatus.Submitted]`:

```csharp
[ExpenseStatus.Submitted] = new Dictionary<ExpenseStatus, UserRole[]>
{
    [ExpenseStatus.Approved] = new[] { UserRole.Manager },
    [ExpenseStatus.Rejected] = new[] { UserRole.Manager },
    [ExpenseStatus.Reimbursing] = new[] { UserRole.Finance }   // <-- the bug
},
```

`ExpenseStateMachine.CanTransition` (and therefore `ExpenseService.Transition`,
which is the *only* place status ever changes — see the class doc comment in
`ExpenseService.cs`) now says a Finance user may move an expense directly from
`Submitted` to `Reimbursing`, skipping `Approved` entirely. `ExpensesController
.Reimburse` (`[Authorize(Roles = "Finance")]`) calls `ExpenseService
.StartReimbursement`, which only checks `ExpenseStateMachine.CanTransition` — it
never checks that the expense passed through `Approved`. Because state is
"whatever `expense.Status` currently is," and the state machine table itself is
the sole authority, adding this one dictionary entry is sufficient: no other
code needed to change for the bypass to work end-to-end.

The `Reimbursing → Reimbursed` system transition
(`BackgroundJobs.ProcessReimbursement`) doesn't re-derive "was this approved" —
it just checks `CanSystemTransition(Reimbursing, Reimbursed)`, which is legal
regardless of how the expense got into `Reimbursing`. So once Finance fast-tracks
it, the background worker happily finishes the job and stamps a
`PaymentReference` — money moves.

## Reproduction

1. Log in as `employee@demo.com`, create + submit an expense (`Draft → Submitted`).
2. Log in as `finance@demo.com`, call `POST /api/expenses/{id}/reimburse`
   directly. `ExpensesController.Reimburse` only requires `Role: Finance`; it
   does not require `Status: Approved` at the HTTP layer (that's meant to be
   enforced by the state machine, and the state machine table is what's broken).
3. `expense.History` now reads `Submitted → Reimbursing → Reimbursed` — never
   `Approved`.

## Why the "pre-approved recurring vendor" framing didn't save it

The commit message describes a *narrow* feature ("pre-approved recurring
vendor charges") but the implementation has no vendor/category/recurrence
predicate anywhere — the state-machine table only understands `(from, to,
role)`, not "and only if X." A reviewer who read the commit message and trusted
it, without checking whether the code actually scoped the exception, would
approve this PR. That's the pattern to flag: **a narrow English description
backing a table-driven change that has no way to express "narrow."**

## Fix

Remove the `[ExpenseStatus.Reimbursing] = new[] { UserRole.Finance }` entry
under `[ExpenseStatus.Submitted]`. If a real fast-track is wanted later, it
needs its own explicit gate (e.g. a `Vendor.IsPreApproved` flag checked in
`ExpenseService.StartReimbursement` *before* calling `Transition`, still going
through `Approved` in the history, or a distinct state/role combination that
can't be reached from `Submitted` by a table edit alone) — not a second edge on
the existing `Submitted` node.

## Regression test

Since the project has zero test files, the first regression test to add is a
`ExpenseStateMachine` characterization/property test (see
`training/review/CHARACTERIZATION_TEST_EXERCISE.md`) asserting the **complete**
set of legal `(from, to, role)` triples matches the documented diagram in the
class doc comment — nothing more, nothing less. A table-driven test like:

```csharp
[Theory]
[InlineData(ExpenseStatus.Submitted, ExpenseStatus.Reimbursing, UserRole.Finance, false)]
// ... one row per (from, to, role) combination that should be legal or illegal
```

would fail immediately on this diff and is cheap to keep green forever, because
the whole point of `ExpenseStateMachine` is "one static table is the single
source of truth" — that's exactly the kind of code a full-combination table
test pins down permanently.

## Model postmortem (for MOCK_DEFENSE-style follow-up)

- **Diagnostic path a senior would take:** don't start in the controller; start
  where "the one place a status actually changes" is documented
  (`ExpenseService.Transition`'s XML doc), then walk backward to
  `ExpenseStateMachine.CanTransition`, then read the `Transitions` table
  top-to-bottom looking for any edge whose *role* doesn't match the state
  diagram in the class doc comment. The diagram in the comment (which still
  matches the *intended* design) is the fastest way to spot the table has
  drifted from it.
- **Why this is worse than a normal bug:** it's a financial control, not a UI
  bug — money moved. The fix needs a corresponding data-correction step (identify
  every `Reimbursed` expense lacking an `Approved` history event, which Audit's
  query above already gives you) in addition to the code fix.
- **What the postmortem should *not* claim:** "add authorization checks in the
  controller as defense in depth" is a reasonable follow-up but is not the root
  cause fix — the controller-level `[Authorize(Roles = "Finance")]` was never
  wrong; the state table was.
