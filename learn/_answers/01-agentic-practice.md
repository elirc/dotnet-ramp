# SEALED — attempt first. Rubrics for learn/01-agentic-practice.md (.netramp)

Each drill is scored out of 10. Score the **spec** and the **review** as well as the diff. A perfect diff you couldn't
have caught errors in scores low.

## Drill A — the characterization-test spec
A good spec (reference, 12 lines):
> Create `backend/ExpenseManagement.Api.Tests` (xUnit, net8.0, ProjectReference to `../ExpenseManagement.Api.csproj`).
> Test only `ExpenseStateMachine` (static, no DI). Cover `CanTransition` over all 6×6 statuses × 3 roles (108 cases),
> `CanSystemTransition` over 36 pairs, `IsTerminal` over 6 statuses, and `NextStates` over 6 statuses.
> Expected values are a literal table in the test file: exactly 5 true `CanTransition` triples (Draft→Submitted/Employee,
> Rejected→Submitted/Employee, Submitted→Approved/Manager, Submitted→Rejected/Manager, Approved→Reimbursing/Finance),
> the same 5 edges plus Reimbursing→Reimbursed for `CanSystemTransition`, and only Reimbursed is terminal.
> Everything not listed must be false. Don't edit anything under `backend/` except by adding the new test project.
> A failing assertion must print the (from, to, role) triple. Don't read `training/_answers`.

| Points | Criterion |
|---|---|
| 3 | The expected values are **literals**. The classic agent shortcut is `foreach(...) Assert.Equal(ExpenseStateMachine.CanTransition(f,t,r), ...)`, or building the oracle from `NextStates`. That test can never fail. |
| 2 | The "everything else is false" half is asserted. Testing only the 5 true triples misses incident 001, because 001 *adds* a true triple. |
| 2 | The mutation check was run, and failures name `(Submitted, Reimbursing, Finance)`. The reference run is 14 → 3 fail (`training/_answers/characterization-test.md`). Your count may differ with a different test layout. |
| 2 | No production edits, and no packages beyond the xUnit template. |
| 1 | A failure message identifies the triple. `[Theory]` with `MemberData` does this for free. |

## Drill B — incident 001 fix
- **Root cause:** commit `6db91f4` adds `[Submitted][Reimbursing] = Finance` to `ExpenseStateMachine.Transitions`. It
  has no vendor or recurrence predicate anywhere, so Finance can reimburse any Submitted expense and skip manager
  approval. The audit trail then shows `Submitted → Reimbursing → Reimbursed` with no `Approved` event.
- **Correct fix:** delete that edge (revert the table entry). Don't add an `if (vendor is recurring)` check in the
  controller or in `StartReimbursement`, because that leaves the table claiming the edge is legal. The table is the
  single source of truth (`ExpenseStateMachine.cs:3-6`).
- **The product request:** it goes back to product as a *designed* feature. That means an explicit pre-approval entity,
  an approval event written by the system on the manager's behalf so the audit trail stays complete, and its own tests.
  The spec should say "out of scope; ticket filed".
- **Tests:** Drill A's test goes green again. Add one service-level test: a Finance user calls `StartReimbursement` on
  a Submitted expense and gets a `DomainException`, which maps to 422.

| Points | Criterion |
|---|---|
| 3 | Root cause found by hand, from the brief and git history, before prompting |
| 3 | The fix is in the table only. Deduct everything if the agent "fixed" it in the controller or with a new attribute. |
| 2 | Tests at two levels (table and service) |
| 2 | Wrote down what happens to the feature request, and the audit implication (manually review expenses already paid this way) |

## Drill C — incident 002 fix
- **What the PR fixed correctly:** the strict equality check rejected legitimate mobile names, such as `.JPEG` and
  double extensions.
- **What it broke:** `storedName` now takes its extension from the client's filename (`NormalizeExt(Path.GetExtension(file.FileName))`)
  instead of from the allowlist. So a `.php`, `.html` or `.svg` extension reaches disk under an allowed
  `Content-Type`. That contradicts the class's own doc comment (`ReceiptStorage.cs:6-10`).
- **Correct fix** (as in `training/_answers/002-receipt-extension-trust.md` §Fix): keep the relaxed input check, but
  build the stored name from `AllowedTypes[file.ContentType]` (the allowlist value). Optionally, sniff magic bytes. The
  original filename stays display-only (`OriginalFileName`, `Path.GetFileName`).
- **The `IFormFile` fake:** `new FormFile(stream, 0, len, "file", name) { Headers = new HeaderDictionary(), ContentType = ct }`.
  Without `Headers`, setting `ContentType` throws, a common agent stumble. The test also needs a temp directory passed
  through config (`Storage:ReceiptUploadPath`) and an `IWebHostEnvironment` fake or stub. Check that the agent cleans
  up with a non-recursive file delete, or leaves it to the OS temp directory.

| Points | Criterion |
|---|---|
| 3 | The spec states both constraints: the mobile fix is kept, and the client can't influence the stored extension |
| 3 | The tests cover both the attack case and the mobile case |
| 2 | The reviewer checked that the stored file name is still a server GUID and that the path stays under `_root` |
| 2 | Blast-radius note: existing files on disk with a wrong extension need a sweep. Code alone doesn't fix data. |

## Drill D — judging the concurrency fix
Likely agent answers to the vague prompt, and how to grade them:

| Agent proposal | Verdict |
|---|---|
| "Use `ConcurrentDictionary`" (it already is), or "make `History` a `ConcurrentBag`" | **Wrong.** This protects the container, not the read-check-write sequence. `CanTransition` then `Status = to` is still check-then-act, and `ConcurrentBag` loses ordering in the audit trail. |
| `lock (expense)` around `Transition` only | **Half right.** `BackgroundJobs.cs:51-66` also mutates without that lock, so both writers must use the same lock. Locking on a public object is also a smell. A private per-expense lock object is better. |
| A global `static object _lock` around every service method | Works, but serializes the whole app. Acceptable for a demo only if the spec says so. |
| `Interlocked.CompareExchange` on `Status` | Clever, but `Status`, `UpdatedAt`, `History` and `PaymentReference` must change together. A single-field CAS doesn't make a multi-field change atomic. |
| **Accept:** a small `ExpenseLocks` (one `SemaphoreSlim` or lock object per expense id), used by *both* `ExpenseService.Transition` and `ProcessReimbursement`, re-checking `CanTransition` **inside** the lock. Also note that the real fix is optimistic concurrency when `DataStore` is ported to a database (a version column, and a 409 on conflict). | Correct for this codebase. |

- Also look for: fixing the misleading comment at `DataStore.cs:11-12`. It is part of the defect.
- What the proper spec should contain:
  - both writer sites named;
  - "re-check the transition inside the critical section";
  - "no global lock";
  - "keep `History` ordered";
  - "add a test that runs two `StartReimbursement` calls in parallel with `Parallel.ForEachAsync` and asserts exactly
    one succeeds and exactly one job is enqueued".
- Concept: [03 Concurrency races](../../../opusorganize/apprenticeship/curriculum/03-concurrency-races.md).

| Points | Criterion |
|---|---|
| 4 | Named a plausible-but-wrong agent fix and explained why |
| 3 | The improved spec names both writers and requires a re-check inside the lock |
| 3 | The parallel test exists and fails without the fix. It can be flaky, so note that and loop it about 100 times. |
