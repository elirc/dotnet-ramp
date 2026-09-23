# Sealed review — ExpenseManagement (.netramp)

Findings ranked by production severity. File:line references are to `main` @
`c5ef2a7` (the committed snapshot, not the incident branches).

## Correctness / data-integrity

1. **[HIGH] Approval bypass is one dictionary edit away and nothing else in the
   codebase would stop it.** `backend/Domain/ExpenseStateMachine.cs:19-43` — the
   `Transitions` table is the *sole* authority for both "is this edge legal" and
   "which role may trigger it" (per the class's own doc comment: "Every status
   change must go through here"). Nothing downstream (`ExpenseService
   .Transition`, `ExpensesController.Reimburse`) re-derives or double-checks
   "did this expense actually pass through Approved." This is exactly the shape
   of bug planted in `training/incidents/001-fast-track-reimbursement.md` — flag
   it here as a *design* risk even before/without that specific incident: a
   table-driven state machine with no test asserting its full contents is one
   accidental (or malicious) entry away from a financial control bypass, and
   there is no test in the repo that would catch it (see finding #14).

2. **[HIGH] Rejected reason isn't cleared on resubmission —
   stale rejection text survives a full re-approval cycle.**
   `backend/Services/ExpenseService.cs:86-101` (`Update`) and `:114-130`
   (`Submit`) — neither clears `expense.RejectionReason`. An employee edits and
   resubmits a rejected expense; `RejectionReason` from the *previous* rejection
   is still returned by the API (`Mappings.cs:33`, `ExpenseResponse
   .RejectionReason`) even while `Status == Submitted` or later `Approved`. A
   manager or Finance reading `GET /api/expenses/{id}` after approval could see
   a stale rejection reason next to an `Approved`/`Reimbursed` status — confusing
   at best, and it pollutes any audit export that includes the field verbatim.

3. **[MEDIUM] Reimbursement uses a same-day, truncated-GUID payment reference
   with no uniqueness guarantee.** `backend/Background/BackgroundJobs.cs:59` —
   `$"PMT-{yyyyMMdd}-{id[..8]}"`. Truncating a GUID to 8 hex chars for a
   human-facing payment reference materially increases collision odds at real
   volume (birthday-bound on a much smaller space than the full GUID), and
   nothing checks `PaymentReference` for uniqueness before it's assigned. Low
   likelihood today (in-memory demo store, low volume) but the exact kind of
   thing that bites once this hits a real database with years of history.

## Concurrency

4. **[HIGH] `DataStore`'s thread-safety comment is misleading — it only covers
   the dictionaries, not the entities inside them.**
   `backend/Storage/DataStore.cs:11-13` claims "ConcurrentDictionary makes
   individual reads/writes thread-safe, which matters because the background
   worker mutates entities on a separate thread." True for `TryAdd`/`TryRemove`/
   indexer assignment on the dictionary itself, **false** for mutating the
   `Expense`/`Receipt` object instances retrieved from it — e.g.
   `ExpenseService.Transition` (`ExpenseService.cs:169-179`) does
   `expense.Status = to; ... expense.History.Add(...)` directly on a shared
   mutable object with no lock, and `BackgroundJobs.ProcessReimbursement`
   (`BackgroundJobs.cs:58-66`) mutates the *same* `Expense` instance from a
   different thread concurrently with any in-flight HTTP request holding a
   reference to it. Two concurrent `POST /reimburse` calls (or a reimburse racing
   an approve/reject) can both pass `CanTransition` before either writes,
   producing a lost update or a doubly-enqueued background job — double payment
   in the worst case. This is a genuine race, not the planted incident; it exists
   independently of incident 001.

5. **[MEDIUM] `List()`'s manager-visibility filter does a per-expense linear
   scan of all users.** `backend/Services/ExpenseService.cs:204-205`
   (`IsDirectReport`) is called once per expense inside the `Manager` branch of
   `List` (`:39-40`) via `_store.Users.GetValueOrDefault(...)`. Fine at demo
   scale (`ConcurrentDictionary` lookup is O(1) so this specific call is cheap),
   but it's evidence of the "in-memory dictionary as the database" design not
   having an indexing story — flag as a design note for whoever ports `DataStore`
   to a real DB (the README explicitly says this port is expected): the
   `Manager`/report-chain visibility filter will need either a denormalized
   `ManagerId` index or a join, not a per-row lookup loop, once this is EF Core
   over Postgres.

## Security

6. **[HIGH] `AuthService.Login`'s "constant-time" comment describes code that
   does the opposite for nonexistent emails.** `backend/Security/AuthService.cs
   :26-30` —
   ```csharp
   if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
       return null;
   ```
   The `||` short-circuits *before* calling `PasswordHasher.Verify` (which does
   100,000 PBKDF2 iterations, `:17` in `PasswordHasher.cs`) whenever `user is
   null`. A request for a nonexistent email returns in microseconds; a request
   for a real email with a wrong password takes the full PBKDF2 duration. That
   timing difference is directly usable to enumerate valid account emails,
   which is precisely the property the adjacent comment claims is avoided. This
   is the canonical "comment describes a security property the code doesn't
   deliver" smell — flag it as such, not just as a bug.

7. **[MEDIUM] No lockout/rate limiting on login.** `AuthController.cs:22-32` /
   `AuthService.Login` — nothing tracks failed attempts per account or per IP.
   Combined with #6, this is a workable (if slow) account-enumeration +
   brute-force surface. Reasonable to leave out of a learning app, but it's the
   kind of gap that should be named explicitly rather than silently accepted,
   given the README frames this as demonstrating "security."

8. **[MEDIUM] Expired sessions are only pruned lazily, on next use — a session
   nobody ever presents again lives in memory forever.**
   `backend/Security/AuthService.cs:41-53` (`ValidateToken`) only removes a
   session from `DataStore.Sessions` when *that specific token* is presented
   again after expiry. A user who logs in and never returns leaves their
   `Session` record (and its `ExpiresAt`) in the dictionary indefinitely — a
   slow, unbounded memory leak with no reaper/background sweep. Same root
   pattern as the never-cleaned-up receipt files (#9): nothing in this app ever
   deletes things that are logically dead, only things that are explicitly
   acted on again.

## Operability / resource management

9. **[HIGH] Uploaded receipt files are never deleted from disk — replacing or
   deleting an expense's receipt only removes the in-memory record.**
   `backend/Services/ReceiptService.cs:43-45` (`UploadAsync`, replacing an old
   receipt) and `backend/Services/ExpenseService.cs:109-111` (`Delete`) both call
   `_store.Receipts.TryRemove(oldId, out _)` — removing the dictionary entry —
   but neither ever calls into `ReceiptStorage` to delete the corresponding file
   under `_root` (`ReceiptStorage.cs`'s `SaveAsync`/`OpenRead` are the only
   methods that touch the filesystem, and nothing calls a delete). Every
   replaced or deleted receipt leaks its file on disk permanently. In a real
   deployment with a persistent volume this is an unbounded storage leak with no
   operator-visible signal until the disk fills up.

10. **[LOW] No correlation/trace ID on error responses**, so a support ticket
    referencing a `500` has nothing to grep server logs for.
    `backend/Middleware/ExceptionHandlingMiddleware.cs:44-58` builds a
    `ProblemDetails` with `Status`/`Title`/`Detail` but no request/trace
    identifier extension, even though ASP.NET Core exposes
    `HttpContext.TraceIdentifier` for exactly this.

11. **[LOW] No `UseHttpsRedirection()` / HSTS in the pipeline.** `Program.cs` — the
    pipeline is `ExceptionHandlingMiddleware → CORS → Authentication →
    Authorization → Controllers` with no HTTPS enforcement. Acceptable behind a
    reverse proxy that terminates TLS and redirects, but nothing in this repo
    documents that assumption, so a reviewer can't tell whether it's intentional.

## Tests (the headline gap)

12. **[HIGH] Zero test files in the entire repository, despite a curriculum
    tree that documents a "testing strategy."** Confirmed via
    `find . -iname "*test*"` returning no `*.cs`/`*.spec.*` files anywhere under
    `backend/` — only the doc filenames
    `docs/upskill/05-quality-engineering/01-testing-strategy.md` and
    `02-writing-tests-here.md` match. This is the single largest gap in the repo
    and the reason findings #1 and #4 above have no safety net: a state machine
    with financial consequences and a shared-mutable-entity concurrency model,
    with no tests at all. This is a textbook old-AI-generated-project smell:
    documentation describing a testing strategy that was never actually
    implemented in code.

13. **[MEDIUM] `README.md`'s own disclosure — "the source is complete but has
    not been compiled/run in this environment" — should be treated as
    unverified-build until proven otherwise**, not as a soft caveat. (Verified
    during this batch: `dotnet build` against `backend/ExpenseManagement.Api
    .csproj` on .NET 8/10 SDK succeeds cleanly with 0 warnings/errors as of this
    review — so the *code* compiles; the README's caveat was about the
    environment it was written in, not a real defect. Still list this as a
    finding because a reviewer should never take "presumably fine" on faith for
    a repo that says outright it was never run.)

14. **[MEDIUM] No test asserts the full contents of `ExpenseStateMachine
    .Transitions`.** Directly related to #1 — the table is small enough (5
    `from` states, ≤2 `to` edges each) that an exhaustive table-driven test
    covering every `(from, to, role)` triple against the documented diagram in
    the class doc comment would be cheap to write and would have caught the
    planted incident 001 bug immediately. See
    `training/review/CHARACTERIZATION_TEST_EXERCISE.md`.

## Maintainability / minor

15. **[LOW] `CreateExpenseRequest` and `UpdateExpenseRequest`
    (`backend/Dtos/Dtos.cs:18-59`) are structurally identical records** — the
    comment even says "kept separate so they can diverge," which is a
    reasonable call but means any future validation change has two places to
    update in lockstep, with nothing enforcing they stay in sync.

16. **[LOW] `ReceiptResponse.ContentType`** (`Mappings.cs:13-15`) echoes back
    whatever `file.ContentType` the client sent, unvalidated beyond the
    allowlist check at upload time — fine today, but worth naming as "this field
    is client-supplied, not derived," since it's easy to later assume it's been
    sanitized/verified because it looks like a controlled enum-ish string.

17. **[LOW] `RequestSizeLimit(6 * 1024 * 1024)` on the upload action**
    (`ReceiptsController.cs:24`) is a magic number computed independently from
    `Storage:MaxReceiptSizeBytes`'s default of `5 * 1024 * 1024`
    (`ReceiptStorage.cs:34`) — the comment ("a little above the configured
    cap") documents the intent but nothing keeps the two constants numerically
    related if either config value changes.

18. **[INFO] Architecture divergence from the rest of the "Net-fleet" of
    portfolio apps**: this is the only one of the ten sibling apps built as a
    single flat project (`backend/{Controllers,Domain,Services,...}`) rather
    than an `Api/Application/Domain/Infrastructure` multi-project split. Not a
    defect — the README calls this out as a deliberate "small dependency
    footprint" choice — but worth naming explicitly in review so a reader
    doesn't assume it's an oversight.

## Old-AI-generated-code smells present (summary)

- **Docs describing testing that was never written** (#12) — the single
  clearest instance in this repo.
- **A comment asserting a security property the adjacent code doesn't actually
  provide** (#6, timing-safe login claim).
- **A thread-safety comment that's true for the container but not for what's
  inside it** (#4).
- **README self-disclosed unverified build status** (#13) — an honest instance
  of this smell rather than a hidden one; worth preserving as a caveat pattern
  to watch for in *other* archive projects that don't disclose it.
