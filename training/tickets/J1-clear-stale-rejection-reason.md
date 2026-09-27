# J1 — A rejected-then-approved expense still shows "Rejection: ..."

**Context.** `ExpenseService.Reject` sets `expense.RejectionReason` (`backend/Services/ExpenseService.cs:144`). Nothing
ever clears it: not `Update` (lines 86-101), not `Submit` (lines 114-130), not `Approve` (lines 132-138). The API
returns the field (`backend/Services/Mappings.cs:33`), and the detail page renders it whenever it's non-empty
(`frontend/src/pages/ExpenseDetailPage.jsx:99`).

**Ticket (Finance lead).** "I'm looking at an expense that's Approved and Reimbursed, but the page still says
'Rejection: missing itemised receipt' in red. Did we pay something we rejected? Please make the page tell the truth."

**Constraints.**
- Don't lose information. The rejection reason is already recorded in `History` as the `note` of the
  `Submitted → Rejected` event (`ExpenseService.cs:145`), and it must stay there.
- No change to the `ExpenseResponse` shape (`backend/Dtos/Dtos.cs`), because the frontend depends on it.
- No new packages.

**Definition of done.**
- `RejectionReason` is `null` whenever `Status` isn't `Rejected`. Decide the single place this is enforced, and justify
  the choice.
- The history entry for the rejection still carries the reason.
- Tests (needs the test project from `learn/` Session 3):
  - reject, then resubmit: `RejectionReason` is null and `History` has the note;
  - reject, resubmit, approve: the reason is null;
  - reject only: the reason is set.
  - Use a real `DataStore`, a real `PolicyEngine` and a no-op `IBackgroundTaskQueue`. Don't use `WebApplicationFactory`.

**Explain before touching.**
1. List every code path that changes `Status`. There are exactly two kinds: service and background. Which ones can
   leave `Rejected`?
2. Option A clears the field in `Submit`. Option B clears it inside `Transition` whenever `to != Rejected`. Option C
   derives it from `History` in `Mappings` and deletes the field. Give one failure mode of each.
3. Why is this a HIGH finding in the sealed review, rather than a cosmetic bug? Think about who reads this field.

<details><summary>Hints</summary>

- `Transition` (`ExpenseService.cs:169-179`) is described as "the one place a status actually changes". Is that true?
  Look at `BackgroundJobs.cs:58-66`.
- Test setup: `new ExpenseService(new DataStore(), new PolicyEngine(), fakeQueue)`, plus users you add to
  `store.Users` with `ManagerId` set.
</details>

Answer: `training/_answers/ticket-J1.md`
