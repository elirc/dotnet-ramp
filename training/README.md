# Training materials — ExpenseManagement (.netramp)

Tier: **DRILL** (Phase 3 review + incidents, plus a small Phase 5 interview
kit). No `ladder/`, `navigation/`, or `agentic/` here — that's LAB-tier scope
and this project didn't get it in this batch (see the batch report for why:
`Desktop\opusorganize\batch1report\projects\netramp.md`).

Everything below references real files in `backend/` on `main` @ `c5ef2a7`
unless a step says otherwise.

## Suggested order (~2.5–3.5 hours total)

1. **`review/EXERCISE.md`** (60 min timeboxed) — blind code review of the whole
   backend. Write your own findings before opening anything in `_answers/`.
2. **`_answers/COMPARE.md`** (15–20 min) — compare against `_answers/review.md`
   and fill in the miss-pattern analysis.
3. **`review/CHARACTERIZATION_TEST_EXERCISE.md`** (30–45 min) — write an
   exhaustive characterization test for `ExpenseStateMachine`. This repo has
   zero test files; this is the first one. Confirm it fails against incident
   001's diff before moving on.
4. **`incidents/001-fast-track-reimbursement.md`** (30–45 min) — an approval
   bypass on `training/incidents/001-fast-track-reimbursement` (branch, based
   on `main`). Diagnose from the brief alone; don't open `_answers/` until
   you've formed a hypothesis and tested it.
5. **`incidents/002-receipt-extension-trust.md`** (20–30 min) — a security
   regression on `training/incidents/002-receipt-extension-trust`. Same rule:
   diagnose first.
6. **`interview/PROJECT_STORIES.md`** and **`interview/TECHNICAL_QUESTIONS.md`**
   (30 min) — do these last, after 1–5, so the stories are grounded in work you
   actually did rather than imagined.

## Layout

```
training/
  README.md                                    (this file)
  review/
    EXERCISE.md                                 blind code review, 60 min
    CHARACTERIZATION_TEST_EXERCISE.md           write tests for ExpenseStateMachine
  incidents/
    001-fast-track-reimbursement.md             symptom brief, no cause
    002-receipt-extension-trust.md              symptom brief, no cause
  interview/
    PROJECT_STORIES.md                          5 STAR prompts, fill in after doing the work
    TECHNICAL_QUESTIONS.md                      10 questions specific to this codebase
  _answers/                                      SEALED — don't open before attempting
    review.md                                   18 ranked findings
    COMPARE.md                                  your-findings-vs-sealed template
    characterization-test.md                     reference test, verified passing + verified
                                                   catches incident 001
    001-fast-track-reimbursement.md             root cause, fix, regression test, postmortem
    002-receipt-extension-trust.md              root cause, fix, regression test, postmortem
```

## Incident branches

Both are based on `main` @ `c5ef2a7` and build clean (`dotnet build`, 0
warnings/0 errors, verified during this batch on .NET SDK 10.0.302 — no
`global.json` pin in this repo):

- `training/incidents/001-fast-track-reimbursement` @ `6db91f4`
- `training/incidents/002-receipt-extension-trust` @ `f9adecc`

Neither branch is merged into `main`. Check one out, or `git show` the diff,
without switching the primary working tree's branch away from `main`.

## What's genuinely unverified

- The reference characterization test in `_answers/characterization-test.md`
  was compiled and run **standalone** (a throwaway `dotnet new xunit` project
  referencing only `Domain/ExpenseStateMachine.cs` + `Enums.cs`, not the real
  `ExpenseManagement.Api.csproj`), and was confirmed to break in exactly the
  expected way against incident 001's diff. It has not been run *inside* an
  actual `backend/ExpenseManagement.Api.Tests` project referencing the real
  csproj — that scaffold doesn't exist in this repo yet and creating it is
  scoped to whoever does exercise 3, not to this batch.
- The rest of the backend (`frontend/`, `Reports` endpoints, background worker
  timing) was read for the review exercise but not exercised end-to-end (no
  server was started; this DRILL tier doesn't include a working-app smoke
  test).

## B08 additions
- New learners should start at `learn/README.md`, the junior on-ramp and study route that leads into this folder.
- `tickets/J1-clear-stale-rejection-reason.md` is a first coding ticket, and the sealed answer is
  `_answers/ticket-J1.md`. Do it after the review.
