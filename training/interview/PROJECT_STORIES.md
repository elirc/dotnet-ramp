# PROJECT_STORIES — ExpenseManagement (.netramp)

Complete each as a STAR story (Situation, Task, Action, Result) **after** you've
actually done the corresponding work in `training/`. Don't write these from
imagination — an interviewer who asks a mechanism follow-up (see
`MOCK_DEFENSE.md` in a richer project's kit, or just self-quiz with
`TECHNICAL_QUESTIONS.md` below) will find the gap immediately if you didn't.

## 1. Debugging story — the approval-bypass incident

Do `training/incidents/001-fast-track-reimbursement.md` first. Then write:
- What did the audit evidence actually tell you, and what did it rule out?
- What was your first hypothesis, and was it right?
- How did you narrow from "somewhere in the approval flow" down to the exact
  dictionary entry in `ExpenseStateMachine.cs`?
- What did you fix, and how did you prove the fix was complete (not just "the
  one reproduction case I tried now fails correctly")?

## 2. Trade-off story — flat architecture vs. the rest of the fleet

This app is deliberately a single flat `backend/{Controllers,Domain,Services,
...}` project instead of the `Api/Application/Domain/Infrastructure`
multi-project split most of its sibling apps use (README: "a deliberately small
dependency footprint"). Pick a side and defend it:
- What does the flat structure cost you as this app grows (name a concrete
  future feature and where it would get awkward)?
- What does the multi-project split cost you that the README is implicitly
  avoiding?
- When would you actually recommend splitting this app, and on what signal?

## 3. Incident story — the receipt extension/content-type regression

Do `training/incidents/002-receipt-extension-trust.md`. Then write:
- How did you distinguish "confirmed exploitable today" from "broken invariant,
  latent risk"? (The sealed answer draws this line explicitly — did you draw it
  yourself before reading it?)
- The regression shipped as a fix for a real user complaint (mobile uploads
  being rejected). How would you have caught this in review without just
  reverting the whole PR and losing the legitimate fix?

## 4. Code-review story — the blind review exercise

Do `training/review/EXERCISE.md`, then `COMPARE.md` against the sealed answer.
Write:
- Your single biggest miss, and the pattern behind it (from `COMPARE.md` §4).
- One finding you rated differently in severity than the sealed review did, and
  why you now agree or still disagree.

## 5. Testing-practice story — characterizing a zero-test codebase

Do `training/review/CHARACTERIZATION_TEST_EXERCISE.md`. Write:
- Why a characterization test was the right first test to write for this class
  specifically (not "testing is good" — the specific property of
  `ExpenseStateMachine` that makes an exhaustive table test cheap and high-value
  here).
- What you'd write next if you had one more hour: a test for `ExpenseService
  .Transition`'s actual usage of the state machine (through the service layer,
  with a real `DataStore`), or something else — and why.
