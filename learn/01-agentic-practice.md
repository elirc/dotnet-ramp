# Agentic practice: directing an agent on .netramp

The loop in every drill:
1. **You** write the spec.
2. The **agent** implements it on a scratch branch.
3. **You** review it adversarially against the checklist below.
4. **Only then** open the sealed rubric in `learn/_answers/01-agentic-practice.md`.

Concept background: [05 Verify before trust](../../opusorganize/apprenticeship/curriculum/05-verify-before-trust.md),
[04 Characterization tests](../../opusorganize/apprenticeship/curriculum/04-characterization-tests.md).

Rules for every drill:
- Work on a branch named `learn/<drill>` off the branch named in the drill. Never commit agent output to `main`.
- Paste your spec into `learn-notes/<drill>-spec.md` on your branch *before* you run the agent. The grade covers the
  spec as much as the code.
- The agent must not open `training/_answers/` or `learn/_answers/`. Say so in your prompt.

---

## Drill A — The characterization test, specified by you (Session 4)
**Source:** `training/review/CHARACTERIZATION_TEST_EXERCISE.md`. **Base:** `main`.
1. Write a spec (15 lines or fewer) covering:
   - the members under test;
   - the input space;
   - where the *expected* values come from;
   - the test project path;
   - the constraint "no production code edits".
2. Give it to the agent. Don't give it the incident branch.
3. Review with the checklist, and pay particular attention to item 3.
4. Mutation check (you do this): apply the incident 001 diff to your branch
   (`git diff main training/incidents/001-fast-track-reimbursement -- backend/Domain/ExpenseStateMachine.cs | git apply`),
   run the tests, and record which ones fail. Then revert.

**Pass bar:** your spec alone leads the agent to a test that fails under the mutation. If it didn't, fix the *spec* and
re-run. Don't fix the test by hand.

## Drill B — Fix incident 001 without over-fixing (Session 6)
**Source:** `training/incidents/001-fast-track-reimbursement.md`. **Base:** branch `training/incidents/001-fast-track-reimbursement`.
1. By hand: find the causing commit (`git log main..training/incidents/001-fast-track-reimbursement`) and write a
   one-sentence root cause.
2. Write the fix spec. It must say:
   - where the fix goes, and where it must **not** go;
   - which test proves it (hint: Drill A's test, plus one service-level test);
   - what to do about the product request the PR was trying to serve.
3. Agent implements. You review.

**Pass bar:** the diff touches `ExpenseStateMachine.cs` and tests only. The Drill A test goes green. Your written note
says what happened to the "fast-track" feature request.

## Drill C — Incident 002: a security fix with a test that proves it (Session 6)
**Source:** `training/incidents/002-receipt-extension-trust.md`. **Base:** branch `training/incidents/002-receipt-extension-trust`.
1. By hand: read `git show f9adecc`. Write what the PR fixed *correctly*, and what it broke.
2. Spec the fix so that both of these hold:
   - the mobile-upload bug the PR was addressing stays fixed;
   - the on-disk extension can no longer be influenced by the client.
3. Agent implements it and adds tests for `ReceiptStorage.SaveAsync`. The agent will need an `IFormFile` fake. Make the
   agent tell you how it builds one.

**Pass bar:** a test uploads `invoice.php` with `Content-Type: image/png`. It asserts either a rejection, or that the
stored name ends in `.png`. It also asserts that `IMG_0001.JPEG` with `image/jpeg` still succeeds.

## Drill D — Judge an agent's concurrency fix (Session 7)
**Source:** `training/_answers/review.md` §Concurrency. Read it only *after* Session 5.
Code: `Storage/DataStore.cs:11-12` (the claim), `Services/ExpenseService.cs:169-179` (request thread) and
`Background/BackgroundJobs.cs:58-66` (worker thread). **Base:** `main`.
1. By hand, *before* prompting: write down which fix you would accept and which fixes you would reject.
2. Prompt the agent with **only**: "Make expense state changes in ExpenseService and BackgroundJobs thread-safe." This
   is deliberately under-specified, so you can see what the agent does with no guidance.
3. Grade the agent's proposal against your list. Then write the spec you *should* have given it, re-run with that
   spec, and compare the two diffs.

**Pass bar:** you can name at least one plausible-looking agent fix that is actually wrong for this codebase, and
explain why.

---

## Verification checklist (use it before trusting any agent diff in this repo)
1. **Scope:** does `git diff --stat` touch only the files the spec named? Were there any edits to `Program.cs`,
   `DataSeeder.cs` or `appsettings*.json` that nobody asked for?
2. **It builds, and the tests run:** `dotnet build backend/ExpenseManagement.Api.csproj` shows 0 errors, and
   `dotnet test` runs tests the agent actually added. Count them yourself.
3. **Tests aren't tautologies:** the expected values are literals that you or the spec wrote down. They must not be
   computed by calling the code under test. For example, `CanTransition` must not be used to generate the
   `CanTransition` oracle.
4. **Mutation:** re-introduce the bug (or flip one edge in `ExpenseStateMachine.Transitions`) and confirm at least one
   test goes red.
5. **Invariants of this codebase still hold:**
   - Every status change goes through `ExpenseService.Transition` or `BackgroundJobs.ProcessReimbursement`, and each
     appends to `History`.
   - Only `Reimbursing → Reimbursed` is system-only.
   - `ReceiptStorage` names files with a server GUID (`ReceiptStorage.cs:56`) and keeps them outside `wwwroot`.
6. **Authorization is enforced in the service, not only in attributes:** controller attributes (`ExpensesController.cs:80,89,98`)
   are defense in depth. The real checks are `GetForActor`, `GetOwned` and `EnsureManagesOwner`. A fix that relies on
   an attribute alone is incomplete.
7. **Comments versus code:** did the agent add or keep a comment that claims a property (for example "thread-safe" or
   "constant-time") that the code doesn't have? This repo already has two of those.
8. **No new dependencies:** the backend has zero NuGet packages on purpose (see README). A new package must be justified
   in the spec.
