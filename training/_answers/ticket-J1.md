# SEALED: J1 answer (clear stale rejection reason)

**Chosen fix: Option B.** In `ExpenseService.Transition`, after the edge check, set
`if (to != ExpenseStatus.Rejected) expense.RejectionReason = null;`. `Reject` still sets the reason *before* calling
`Transition` (line 144), and `Transition` to `Rejected` leaves it alone.

**Why B.** The invariant "reason is set iff status is Rejected" is about status changes, so it belongs in the one
service method that changes status. It covers `Submit`, and any future edge out of `Rejected`, without anyone having
to remember it.

**The failure modes the learner should name.**
- **A (clear in `Submit`):** it's correct today, but the next edge out of `Rejected` (say a Finance "reopen") would
  forget to clear it. The invariant would live in a caller, not at the choke point.
- **B (clear in `Transition`):** `BackgroundJobs.ProcessReimbursement` bypasses `Transition`
  (`BackgroundJobs.cs:58-66`). That's harmless here, because it can only start from `Reimbursing`, where the reason is
  already null. But the "one place" comment at line 168 is false, so say that in the PR description.
- **C (derive from `History`):** it removes duplicated state, which is the cleanest option in the long run. But it
  changes how `Mappings` works, it's O(history) per read, and it quietly breaks anything that sets the field directly.
  It's a bigger change than a J ticket should be, so propose it as a follow-up.

**Why it's HIGH.** Finance and audit read this field on paid expenses. A red "Rejection" next to "Reimbursed" either
triggers a false fraud investigation, or trains people to ignore the field. Stale state that looks authoritative is
worse than missing state.

**Reference tests.**
```csharp
[Fact] public void Resubmit_clears_reason_but_history_keeps_it()
{
    var (svc, store, emp, mgr) = Fixture();          // real DataStore/PolicyEngine, no-op queue
    var e = svc.Create(emp, new CreateExpenseRequest { Merchant = "Cafe", Description = "lunch", Amount = 20m,
        Currency = "USD", Category = ExpenseCategory.Meals, ExpenseDate = Today() });
    svc.Submit(emp, e.Id);
    svc.Reject(mgr, e.Id, "missing itemised receipt");
    svc.Submit(emp, e.Id);
    Assert.Null(e.RejectionReason);
    Assert.Contains(e.History, h => h.To == ExpenseStatus.Rejected && h.Note == "missing itemised receipt");
}
```
`CreateExpenseRequest` is an init-property record (`backend/Dtos/Dtos.cs:18`). The snippet wasn't compiled (docs-only
pass), so treat it as a shape, not something to paste.

**Grading.** 4 points for the choke-point placement plus the justification, 3 for the three tests, 3 for noticing that
the line-168 "one place" comment is inaccurate.
