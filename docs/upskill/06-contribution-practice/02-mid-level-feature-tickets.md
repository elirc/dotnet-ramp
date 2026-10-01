# Mid-Level Feature Tickets

Cross-layer work. Each needs a design note, touches schema/API/UI/tests, and has a risk +
rollback line. Template: **Story · Design notes · Files · Risk/Rollback · Tests · Done when.**

---

### Ticket 1 — Expense comments / approver notes
**Story:** as a manager, I want to leave a note on approval so the employee gets context.
**Design:** add an embedded `List<Comment>` on `Expense` (like `History`); endpoint
`POST /api/expenses/{id}/comments`; authorize via existing `GetForActor`.
**Files:** `Domain/Expense.cs`, `ExpenseService`, `ExpensesController`, `Dtos`, `Mappings`, detail page.
**Risk/Rollback:** additive; rollback = remove endpoint + field. Low blast radius.
**Tests:** only owner/manager/finance who can view can comment; comment appears in response.
**Done when:** comments render in the timeline and are authz-scoped.

### Ticket 2 — Multi-level approval (amount-based escalation)
**Story:** expenses over $1,000 need a second approver.
**Design:** the `HIGH_VALUE` warning already exists; add a state or an `ApprovalsRequired`
count and track approvals in `History`. Likely a new status `PendingSecondApproval`.
**Files:** state machine (new edges), `ExpenseService.Approve`, DTO, UI.
**Risk/Rollback:** touches the state machine — the spine. Feature-flag the threshold; rollback by setting threshold to ∞.
**Tests:** $1,001 needs two approvals; $999 needs one. **Invariant:** can't reach `Approved` with one approval over threshold.
**Done when:** the escalation path is enforced and audited.

### Ticket 3 — CSV export of the reports view
**Story:** finance wants to export the summary.
**Design:** new endpoint returning `text/csv`; reuse `ReportingService` (so scoping holds).
**Files:** `ReportsController`, maybe a small CSV writer (built-in `string.Join`).
**Risk/Rollback:** read-only; rollback = remove endpoint.
**Tests:** an employee's export contains only their rows (scoping). No CSV injection (escape leading `=,+,-`).
**Done when:** export matches on-screen data and respects role scope.

### Ticket 4 — Manager dashboard: pending-approval queue
**Story:** managers want a focused queue of `Submitted` expenses for their reports.
**Design:** the API already supports `?status=Submitted` scoped by role — mostly frontend.
**Files:** new page/route, nav link, reuse `client.js`.
**Risk/Rollback:** frontend-only; rollback = remove route.
**Tests (manual/E2E):** a manager sees only their team's submitted items.
**Done when:** the queue is one click from the nav.

### Ticket 5 — Configurable policy limits (from config, not constants)
**Story:** finance wants to tune category limits without a redeploy of logic.
**Design:** move `CategoryLimits`/thresholds from constants into `IOptions`-bound config.
**Files:** `PolicyEngine` (inject options), `appsettings.json`, `Program.cs`.
**Risk/Rollback:** changes a pure class into a configured one; keep defaults equal to today's constants. Rollback = revert binding.
**Tests:** defaults match current behavior; overriding config changes a flag.
**Done when:** limits are config-driven with the same defaults.

### Ticket 6 — Email-style notifications (stub) on approval/rejection
**Story:** notify the employee when their expense is approved/rejected.
**Design:** an `INotifier` enqueued as a background job (reuse the queue); start with a
log-only implementation. Demonstrates the side-effect-via-queue pattern.
**Files:** new `Notifications/`, enqueue in `Approve`/`Reject`, `Program.cs`.
**Risk/Rollback:** additive job; rollback = stop enqueuing.
**Tests:** approving enqueues exactly one notification; failure in the notifier doesn't fail the approval.
**Done when:** the log shows a notification per transition.

### Ticket 7 — Replace polling with Server-Sent Events for receipt status
**Story:** the detail page should update without polling.
**Design:** an SSE endpoint that pushes when a job completes; fall back to polling.
**Files:** new controller action, frontend `EventSource`, worker signals completion.
**Risk/Rollback:** connection management complexity; keep polling as fallback so rollback is trivial.
**Tests:** status updates arrive without a manual refresh.
**Done when:** receipt flips to Processed live.

### Ticket 8 — Pagination for the expenses list
**Story:** the list should page when data grows.
**Design:** `?page=&pageSize=`; return total count. Trivial in memory; designed to map to DB `Skip/Take` later.
**Files:** `ExpenseService.List`, controller, response envelope DTO, UI controls.
**Risk/Rollback:** changes the list response shape — **a contract change**; version or keep additive. Rollback = ignore params.
**Tests:** page boundaries; scoping still applied before paging.
**Done when:** UI pages and counts are correct.

### Ticket 9 — Soft-delete instead of hard-delete for drafts
**Story:** deleted drafts should be recoverable/auditable.
**Design:** add `DeletedAt?`; filter it out of queries; keep the row.
**Files:** `Expense`, `ExpenseService.Delete`/`List`, maybe an "undo".
**Risk/Rollback:** every query must filter deleted — easy to miss one (a leak). Rollback = revert.
**Tests:** deleted drafts don't appear in any role's list; a missed filter is caught.
**Done when:** delete is reversible and invisible everywhere.

### Ticket 10 — Account lockout / login rate limiting
**Story:** slow down brute-force login.
**Design:** track failed attempts per email/IP (in `DataStore`), lock for N minutes; or use built-in rate limiting middleware.
**Files:** `AuthService`/`AuthController`, maybe `Program.cs` rate limiter.
**Risk/Rollback:** could lock out legit users — tune thresholds; feature-flag.
**Tests:** N failures → locked; success resets.
**Done when:** repeated bad logins are throttled.

### Ticket 11 — Currency conversion to a base currency for reports
**Story:** mixed-currency expenses should roll up in one base currency.
**Design:** a (stubbed) `IRateProvider`; store the converted amount or convert at report time.
**Files:** `ReportingService`, a rate provider, config for base currency.
**Risk/Rollback:** rounding + rate-staleness correctness; keep raw amounts intact. Rollback = report raw.
**Tests:** known rates produce known totals; rounding is consistent.
**Done when:** reports show a single-currency total.

### Ticket 12 — Make `DataStore` an interface (prep for EF Core)
**Story:** decouple services from the concrete store.
**Design:** extract `IDataStore`; register the in-memory impl; no behavior change.
**Files:** `Storage/`, `Program.cs`, service constructors.
**Risk/Rollback:** wide but mechanical; rollback = revert. **This unblocks the senior EF Core project.**
**Tests:** existing behavior unchanged (characterization tests first!).
**Done when:** services depend on `IDataStore`, all flows still work.
