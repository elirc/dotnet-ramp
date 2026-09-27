# Learn here first: .netramp (ExpenseManagement)

This is the **entry point** of the B01 .NET route for someone with shaky fundamentals. It is one small ASP.NET Core 8
project (`backend/ExpenseManagement.Api.csproj`), has zero NuGet packages, keeps its data in memory, and has no tests yet.
You can read the whole backend in an afternoon. Start here, not with netdrop or netintercom.

## What this app is and why it's in the portfolio
- An expense-approval app: employees file expenses, managers approve them, and Finance reimburses them. The lifecycle
  is a table-driven state machine (`backend/Domain/ExpenseStateMachine.cs`), and the background worker marks items paid
  (`backend/Background/BackgroundJobs.cs:40-69`).
- It's in the portfolio because it is the smallest app that still has the things real backends have: roles and
  authorization (`backend/Services/ExpenseService.cs:190-205`), a policy engine (`backend/Services/PolicyEngine.cs`),
  file uploads (`backend/Services/ReceiptStorage.cs`) and a background queue (`backend/Background/`). It also carries two
  comments that claim more than the code does, which makes it good practice for "verify, don't trust".
- The existing drill material lives in `training/` (blind review, characterization test, 2 incidents, mini interview kit).
  This `learn/` folder is the on-ramp *to* that material.

## Known state of main
- **It builds.** Both incident branches build clean with 0 warnings and 0 errors on SDK 10.0.302 (source:
  `Desktop\opusorganize\batch1report\projects\netramp.md` §Verification). This repo has no `global.json`.
- **There are zero test files.** The characterization-test reference in `training/_answers/characterization-test.md`
  was run in a standalone scratch project: 14/14 pass on `main`, and 3 fail when incident 001's diff is applied. It has
  never been run inside a real `backend/ExpenseManagement.Api.Tests` project, because that project doesn't exist yet.
  Creating it is your job in Session 4.
- Nobody ran the app for this pass. Anything below about runtime behavior comes from reading the code and is marked
  UNVERIFIED where it matters.

## Prerequisites
These are the fundamentals you need before starting. Each one points at a file here, and the harder concepts link to
the central curriculum.
1. **C# types: enums, records, classes, `init` vs `set`, nullable `?`.** Read `backend/Domain/Enums.cs` and
   `backend/Domain/Expense.cs` (the whole of both, 100 lines). On-ramp exercise 1 checks this.
2. **Dictionaries and LINQ (`Where`, `Select`, `OrderByDescending`, `TryGetValue`).** Read `ExpenseService.List`
   (`backend/Services/ExpenseService.cs:30-52`) and `ExpenseStateMachine.CanTransition` (lines 51-54).
3. **How an ASP.NET Core request flows: DI lifetimes, middleware order, `[Authorize]`, controllers.** Read
   `backend/Program.cs` (58 lines). On-ramp exercises 3 and 4 check this.
4. **What a unit test is and why a characterization test is different.** Read
   [04 Characterization tests](../../opusorganize/apprenticeship/curriculum/04-characterization-tests.md).
5. **Reading code you didn't write, including AI-written code.** Read
   [05 Verify before trust](../../opusorganize/apprenticeship/curriculum/05-verify-before-trust.md).
6. (Needed for Session 6) **Check-then-act races.** Read
   [03 Concurrency races](../../opusorganize/apprenticeship/curriculum/03-concurrency-races.md).

## Study route
Total: about 9–11 hours over 8 sessions. Don't open `_answers/` (either `learn/_answers/` or `training/_answers/`) until
the session tells you to.

### Session 1 — Read it, then try to run it (60 min)
- **Files/branches:** `README.md` (the run instructions and demo logins), `backend/Program.cs`, `backend/Storage/DataSeeder.cs`, `backend/Domain/*.cs`. Branch `main`.
- **Do by hand:**
  - Draw the state diagram from `ExpenseStateMachine.cs:19-43` on paper, *without* copying the ASCII art at lines 8-11.
  - List the 4 seeded users and the 2 seeded expenses (`DataSeeder.cs:18-53`).
- **Delegate, then verify:** ask an agent for the exact commands to run the backend and frontend (`dotnet run` in `backend/`, `npm install && npm run dev` in `frontend/`). Check its answer against `README.md` yourself before running anything.
- **Exit criterion:** from memory, you can say which role can move an expense out of each status, and which transition no human can trigger (`Reimbursing → Reimbursed`, system-only).

### Session 2 — On-ramp exercises 1–4 (75 min)
- **Files/branches:** `learn/00-onramp.md` exercises 1–4.
- **Do by hand:** all of it. These are prediction exercises. Write your prediction *before* you check it.
- **Delegate, then verify:** nothing. The point of this session is to build your own mental model.
- **Exit criterion:** at least 80% of your predictions match `learn/_answers/00-onramp.md`. You can also trace `POST /api/expenses/{id}/approve` from `frontend/src/api/client.js` to `ExpenseService.Transition` naming every file on the way, without notes.

### Session 3 — On-ramp exercises 5–8 and your first test (75 min)
- **Files/branches:** `learn/00-onramp.md` exercises 5–8, `backend/Services/PolicyEngine.cs`.
- **Do by hand:**
  - Predict the policy flags for each case.
  - Write the *list* of test cases for exercise 7 yourself.
- **Delegate, then verify:**
  - Have an agent scaffold `backend/ExpenseManagement.Api.Tests` (xUnit, referencing the API csproj) on a scratch branch.
  - Check that the scaffold has no production edits, no packages beyond xUnit, and that the test project really references `ExpenseManagement.Api.csproj`.
- **Exit criterion:** your `PolicyEngine` boundary test (Amount = 25m with no receipt) passes on `main`. Then make it fail by changing `>` to `>=` at `PolicyEngine.cs:50` in a throwaway edit, and revert the edit.

### Session 4 — The characterization test (90 min)
- **Files/branches:** `training/review/CHARACTERIZATION_TEST_EXERCISE.md`, then `learn/01-agentic-practice.md` Drill A.
- **Do by hand:** write the spec for the test: which members to cover, the 3 × 6 × 6 input space, and how the *expected* values are written down.
- **Delegate, then verify:**
  - Have the agent write the test.
  - Check it against the Drill A checklist. The trap to look for: expected values that are computed by calling `CanTransition` itself.
- **Exit criterion:** the test passes on `main`. After you apply incident 001's diff (`git diff main training/incidents/001-fast-track-reimbursement -- backend/Domain/ExpenseStateMachine.cs`), it fails with exactly the `(Submitted, Reimbursing, Finance)` triple named. The reference result is 14 pass, then 3 fail.

### Session 5 — Blind review (60 + 20 min)
- **Files/branches:** `training/review/EXERCISE.md`, then `training/_answers/COMPARE.md`, then `training/_answers/review.md`.
- **Do by hand:** the whole review. Don't let an agent do this, because the skill you're building is spotting the problems yourself.
- **Delegate, then verify:** *after* you finish, ask an agent to review the same code independently. Score its findings against `training/_answers/review.md` just as you scored your own. Note which real findings it missed and which findings it invented.
- **Exit criterion:** you found at least 6 of the 18 sealed findings, including one of the two "comment claims a property the code doesn't have" findings. You can also name one agent finding that was wrong, and say why.

### Session 6 — Incidents (2 × 45 min)
- **Files/branches:**
  - `training/incidents/001-fast-track-reimbursement.md` on branch `training/incidents/001-fast-track-reimbursement`.
  - `training/incidents/002-receipt-extension-trust.md` on branch `training/incidents/002-receipt-extension-trust`.
  - Open the branches without switching your main tree, for example with `git worktree add <scratch> <branch>` outside the repo, or with `git show`.
- **Do by hand:** diagnose each incident from the brief alone. Write a hypothesis, find the diff that confirms it, and write down the blast radius.
- **Delegate, then verify:** the fix and the regression test, via `learn/01-agentic-practice.md` Drills B and C.
- **Exit criterion:** for each incident, your root-cause sentence matches the sealed `training/_answers/00N-*.md`. Your regression test fails on the incident branch and passes after the fix.

### Session 7 — Your first ticket plus a concurrency read (75 min)
- **Files/branches:**
  - `training/tickets/J1-clear-stale-rejection-reason.md` (added in B08).
  - `learn/01-agentic-practice.md` Drill D (the `DataStore` race).
- **Do by hand:**
  - Write the J1 acceptance tests first.
  - For Drill D, decide which fix you would accept before you see what the agent proposes.
- **Delegate, then verify:**
  - Have an agent implement J1.
  - Have an agent propose a fix for the race. Grade that proposal against `learn/_answers/01-agentic-practice.md` Drill D.
- **Exit criterion:** the J1 tests pass. You can explain in 3 sentences why "it's a `ConcurrentDictionary`" does not make `Expense.History.Add` thread-safe.

### Session 8 — Interview kit (45 min)
- **Files/branches:** `training/interview/PROJECT_STORIES.md`, `training/interview/TECHNICAL_QUESTIONS.md`.
- **Do by hand:**
  - Fill in the STAR stories from work you actually did in Sessions 3–7.
  - Answer the 10 questions out loud.
- **Delegate, then verify:** have an agent play the interviewer and ask follow-ups on your stories. Reject any follow-up question that isn't grounded in this code.
- **Exit criterion:** you can answer TQ 1, 5, 6 and 7 without notes, citing file names.

## Do vs. delegate (summary)
| Always by hand | Delegate, then verify |
|---|---|
| Predictions, traces, diagrams (Sessions 1–3) | Test-project scaffolding (Session 3) |
| The spec for every test and fix | Writing the data-driven test body (Session 4) |
| Blind review and incident diagnosis | Fix code for incidents and J1 |
| Deciding whether agent output is acceptable | A second-opinion review, which you then grade |

## How you know you're done here
- A real `backend/ExpenseManagement.Api.Tests` project exists on your own branch. It contains the characterization test
  and at least one `PolicyEngine` test, and it catches incident 001.
- You scored at least 6/18 on the blind review, and both incident root causes match the sealed answers.
- You can explain every DI lifetime in `Program.cs:16-26`, and the HTTP status each exception type maps to
  (`ExceptionHandlingMiddleware.cs`).

## Next repo
**`Desktop\.netstripe`.** It's the same "minimal backend, zero tests" shape, but the domain is money, it teaches
idempotency, and **its `main` doesn't compile**, so the first lesson there is diagnosing a broken build.
