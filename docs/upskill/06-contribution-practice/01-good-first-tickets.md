# Good First Tickets

Condensed template per ticket: **Difficulty · Skills · Read first · Files · Plan · Rejected
if**. Spread across the whole repo. None require new dependencies unless noted.

---

### Ticket 1 — Add a `Training` expense category
**Easy · enums + UI · Read:** [`Enums.cs`](../../../backend/Domain/Enums.cs), [`PolicyEngine` L18-29](../../../backend/Services/PolicyEngine.cs#L18-L29), [`format.js`](../../../frontend/src/util/format.js)
**Plan:** add enum value → add a `CategoryLimits` entry → add to `CATEGORIES`.
**Rejected if:** you forget the limit (it'll default to no limit) or the UI list, causing a mismatch.

### Ticket 2 — Show the policy limit in the UI tooltip
**Easy · React · Read:** [`ExpenseDetailPage` L104-116](../../../frontend/src/pages/ExpenseDetailPage.jsx#L104-L116)
**Plan:** the flag message already contains the limit; ensure it renders (it does) and add a title attr. Mostly a reading exercise.
**Rejected if:** you hardcode limits client-side (they belong server-side).

### Ticket 3 — Add a `Cancelled` terminal-ish state reachable from Draft
**Easy/Medium · state machine · Read:** [`ExpenseStateMachine`](../../../backend/Domain/ExpenseStateMachine.cs)
**Plan:** add enum, add `Draft → Cancelled` (Employee) edge, expose a `cancel` action + button.
**Rejected if:** you allow cancelling an already-`Reimbursed` expense, or skip the audit history.

### Ticket 4 — Make `ProcessReceipt` idempotent
**Easy · async · Read:** [`BackgroundJobs` L16-33](../../../backend/Background/BackgroundJobs.cs#L16-L33)
**Plan:** skip if `receipt.Status == Processed`. Mirror the guard style of `ProcessReimbursement`.
**Rejected if:** you add the guard but break the happy path.

### Ticket 5 — Validate currency against an allowlist
**Easy · validation · Read:** [`Dtos.cs` CreateExpenseRequest](../../../backend/Dtos/Dtos.cs)
**Plan:** currency is free 3-char text today; restrict to e.g. USD/EUR/GBP via a custom check or enum.
**Rejected if:** you break existing `USD` data or hardcode in the UI only.

### Ticket 6 — Add `dana@demo.com` sample expenses to the seeder
**Easy · data · Read:** [`DataSeeder`](../../../backend/Storage/DataSeeder.cs)
**Plan:** add a couple of expenses for the second employee so manager views show two people.
**Rejected if:** ids collide or you seed into a non-empty store.

### Ticket 7 — Surface `extractedAmount` mismatch as a warning
**Medium · policy · Read:** [`PolicyEngine`](../../../backend/Services/PolicyEngine.cs), [`Receipt`](../../../backend/Domain/Receipt.cs)
**Plan:** if `ExtractedAmount` differs from `Amount` by > X%, add a Warning flag. Pass the receipt into `Evaluate` (signature change — small ripple).
**Rejected if:** you make it Blocking (OCR is fake/unreliable) or fetch the receipt inside the pure engine.

### Ticket 8 — Add a "Reimbursed on" timestamp
**Easy · domain + DTO · Read:** [`Expense`](../../../backend/Domain/Expense.cs), [`BackgroundJobs` L58-66](../../../backend/Background/BackgroundJobs.cs#L58-L66), [`Mappings`](../../../backend/Services/Mappings.cs)
**Plan:** add `ReimbursedAt?`, set it in the job, expose in `ExpenseResponse`, show in UI.
**Rejected if:** you set it anywhere but the system transition.

### Ticket 9 — Empty-state copy + loading spinners polish
**Easy · React/UX · Read:** [`ExpensesPage`](../../../frontend/src/pages/ExpensesPage.jsx)
**Plan:** improve the "No expenses found" / loading states.
**Rejected if:** scope-creeps into restyling everything.

### Ticket 10 — Add a date-range filter to the expenses list UI
**Medium · React + existing API · Read:** [`ExpensesController.List` L28-32](../../../backend/Controllers/ExpensesController.cs#L28-L32) (the API already accepts `from`/`to`)
**Plan:** add two date inputs, pass as query params (the wrapper + endpoint already support it).
**Rejected if:** you add backend code — it's already there; this is frontend-only.

### Ticket 11 — Return 400 (not 500) when uploading with no file
**Easy · API · Read:** [`ReceiptsController.Upload` L25-28](../../../backend/Controllers/ReceiptsController.cs#L25-L28)
**Plan:** confirm the null-check returns `BadRequest`; add a test/repro. (Mostly verification.)
**Rejected if:** you change the happy path.

### Ticket 12 — Add `public partial class Program {}` for testability
**Easy · testing seam · Read:** [`Program.cs`](../../../backend/Program.cs)
**Plan:** one line at the end so `WebApplicationFactory<Program>` works. Enables Recipe 7.
**Rejected if:** you change runtime behavior.

### Ticket 13 — Log a warning when a session is rejected as expired
**Easy · observability · Read:** [`AuthService` L46-49](../../../backend/Security/AuthService.cs#L46-L49)
**Plan:** add an `ILogger` and a debug/info line. (Adds a constructor dependency.)
**Rejected if:** you log the token value (security — see [Kata 7](../04-code-reading-gym/04-review-katas.md)).

### Ticket 14 — Add a health-check endpoint
**Easy · ops · Read:** [`Program.cs`](../../../backend/Program.cs)
**Plan:** `AddHealthChecks()` + `MapHealthChecks("/health")` (built-in, no packages).
**Rejected if:** you pull in a health-check NuGet package unnecessarily.

### Ticket 15 — Sort the expenses list by a chosen column
**Medium · React · Read:** [`ExpensesPage`](../../../frontend/src/pages/ExpensesPage.jsx)
**Plan:** client-side sort on the already-fetched list.
**Rejected if:** you add server sorting for a list that's already small and fully returned.

### Ticket 16 — Pin the SDK with `global.json`
**Easy · tooling · Read:** [02/04](../02-stack-and-language-mastery/04-tooling-and-build-system.md)
**Plan:** add `global.json` pinning the .NET 8 SDK for reproducible builds.
**Rejected if:** you pin a version not installed on CI/dev.

### Ticket 17 — Add `RejectionReason` clearing on resubmit
**Medium · domain · Read:** [`ExpenseService.Submit` / Reject](../../../backend/Services/ExpenseService.cs#L114-L147)
**Plan:** when a Rejected expense is resubmitted, clear the stale `RejectionReason`.
**Rejected if:** you clear it on the wrong transition or lose audit history.

### Ticket 18 — Write `CONTRIBUTING.md`
**Easy · docs · Read:** this whole module
**Plan:** document setup, run, test, PR conventions, the "go through the service" rule.
**Rejected if:** it contradicts actual commands (verify them first).

---

**Self-grade any ticket Strong if:** the diff is small, follows an existing pattern, includes
a test or a manual repro, and the PR description says *what could go wrong*.
