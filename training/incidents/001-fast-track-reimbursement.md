# Incident 001 — Finance reimbursing expenses that were never approved

**Branch:** `training/incidents/001-fast-track-reimbursement` (based on `main` @ `c5ef2a7`; not published to the remote, so see the status note in `training/README.md` to recreate it)
**Severity:** SEV-2 (financial control bypass)
**Reported by:** Internal Audit, during the monthly spend reconciliation

## What Audit saw

While reconciling last month's reimbursements against manager approvals, Audit
pulled every expense with `Status = Reimbursed` and cross-referenced its history
against the corresponding `Approved` event. Three expenses (all filed by the same
employee, all processed by the same Finance user) show a `Reimbursed` status but
**no `Approved` event anywhere in their history** — the audit trail jumps straight
from `Submitted` to `Reimbursing` to `Reimbursed`.

Finance says they didn't do anything unusual: "I just hit reimburse on the ones in
my queue like normal."

## What's in the queue right now

Support also has an open ticket from a different employee's manager:

> "I have three expense reports sitting in my 'awaiting my approval' list that my
> dashboard number says are still pending, but the employee is telling me they
> already got paid. I haven't approved anything for them this week."

## Evidence available to you

- `GET /api/expenses/{id}` for the three flagged expenses (returns `history`,
  `status`, `paymentReference`)
- The Finance user's session is legitimate — no sign of a leaked/shared token.
- Nothing in the deploy log points at infrastructure — the last backend deploy was
  the routine weekly release two days before Audit's report period started.

## Your task

1. Reproduce the sequence of role-gated actions that gets an expense from
   `Submitted` to `Reimbursed` without an `Approved` event ever being recorded.
2. Identify the exact place in the codebase that permits it.
3. Write the fix as a minimal diff.
4. Write a regression test (or a characterization test if you're doing the
   companion exercise in `training/review/`) that would have caught this before
   it shipped.

No hints beyond this brief — the cause is not stated above. Sealed reference
analysis: `training/_answers/001-fast-track-reimbursement.md` (don't open it
until you've formed your own hypothesis).
