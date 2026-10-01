# Key Flows

Six end-to-end traces. Read each with the files open. They deliberately rotate across the
seams you must understand: an auth boundary, an API write, persistence, file upload, a
background job, and a role-gated approval.

Coverage map: **client/React** → Flow 1 & 3; **ASP.NET API** → Flow 2; **persistence** →
Flow 2 & 5; **background/async** → Flow 3 & 5; **auth boundary** → Flow 1 & 4.
*(There is no testing/CI flow because the repo has none yet — building one is the first
exercise in [05/02](../05-quality-engineering/02-writing-tests-here.md).)*

---

## Flow 1: Login and authenticated bootstrap

**Why it matters:** every other flow depends on the `actor`. This shows how an opaque token
becomes an authorized `User` on each request — the foundation of the security model.

**Open first:** [`AuthContext.jsx`](../../../frontend/src/auth/AuthContext.jsx) and
[`TokenAuthenticationHandler.cs`](../../../backend/Security/TokenAuthenticationHandler.cs).

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Client | [`LoginPage.jsx`](../../../frontend/src/pages/LoginPage.jsx) | User submits email+password | `{email, password}` | — |
| 2 | Client | [`AuthContext.login` L33-38](../../../frontend/src/auth/AuthContext.jsx#L33-L38) | POST `/api/auth/login`, stores token | `LoginResponse` | token in `localStorage` (XSS-readable) |
| 3 | API | [`AuthController.Login`](../../../backend/Controllers/AuthController.cs) | Calls `AuthService.Login` | — | — |
| 4 | Service | [`AuthService.Login` L24-36](../../../backend/Security/AuthService.cs#L24-L36) | Verifies PBKDF2 hash, creates session | `Session(token, userId, expiry)` | — |
| 5 | Client | [`client.js` L22-23](../../../frontend/src/api/client.js#L22-L23) | Every later request sends `Authorization: Bearer …` | header | — |
| 6 | API | [`TokenAuthenticationHandler` L30-55](../../../backend/Security/TokenAuthenticationHandler.cs#L30-L55) | Resolves token → `ClaimsPrincipal` with role claim | claims | — |
| 7 | API | [`UserContext.RequireUser` L35-40](../../../backend/Security/UserContext.cs#L35-L40) | Claims → the `User` entity for the service layer | `User` | — |

**Validation & authorization:** password check is constant-time
([`PasswordHasher.Verify`](../../../backend/Security/PasswordHasher.cs)); `[Authorize]` on
controllers triggers the handler; role claim set at [handler L49](../../../backend/Security/TokenAuthenticationHandler.cs#L49).

**Persistence & side effects:** a `Session` is written to `DataStore.Sessions`
([`AuthService` L34](../../../backend/Security/AuthService.cs#L34)).

**Tests:** None found (no test project in repo).

**Juniors miss:** that the token is just a dictionary key — there's no signature; the
server *must* keep the session to validate it. **Seniors notice:** sessions never get
pruned except lazily on access ([`AuthService` L46-49](../../../backend/Security/AuthService.cs#L46-L49)),
so expired sessions for users who never return leak memory — fine here, a real concern at scale.

**Drill:** Trace what happens on app reload when a token exists but the session expired.
Which line clears it, and what does the user see? (Hint: [`AuthContext` L14-31](../../../frontend/src/auth/AuthContext.jsx#L14-L31).)
**Self-grade:** Basic = found the `me` call. Solid = explained the 401→clear→login redirect. Strong = noted there's no token refresh.

---

## Flow 2: Create then submit an expense (the canonical API write)

**Why it matters:** this is the template every state-changing endpoint follows: controller
→ service → policy → state machine → store.

**Open first:** [`ExpenseService.cs`](../../../backend/Services/ExpenseService.cs).

**Trace (submit):**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Client | [`ExpenseDetailPage` L144](../../../frontend/src/pages/ExpenseDetailPage.jsx#L144) | POST `/api/expenses/{id}/submit` | — | — |
| 2 | API | [`ExpensesController.Submit` L71-77](../../../backend/Controllers/ExpensesController.cs#L71-L77) | Resolves actor, calls service | — | — |
| 3 | Service | [`Submit` L114-117](../../../backend/Services/ExpenseService.cs#L114-L117) | `GetOwned` (ownership check), `RefreshPolicy` | `Expense` | — |
| 4 | Service | [`RefreshPolicy` L159-164](../../../backend/Services/ExpenseService.cs#L159-L164) | Recompute policy flags from current state | `List<PolicyFlag>` | — |
| 5 | Service | [`Submit` L119-126](../../../backend/Services/ExpenseService.cs#L119-L126) | If any **Blocking** flag → throw `DomainException(422)` with reasons | — | — |
| 6 | Service | [`Transition` L169-178](../../../backend/Services/ExpenseService.cs#L169-L178) | State machine check, set status, append history | `ExpenseEvent` | mutation of shared object (no lock) |
| 7 | API | [`Mappings.ToResponse` L17-42](../../../backend/Services/Mappings.cs#L17-L42) | Entity → DTO incl. `availableActions` | `ExpenseResponse` | — |

**Validation & authorization:** ownership in [`GetOwned` L181-188](../../../backend/Services/ExpenseService.cs#L181-L188);
policy gate in [`Submit` L119](../../../backend/Services/ExpenseService.cs#L119); transition legality in
[`ExpenseStateMachine.CanTransition` L51-54](../../../backend/Domain/ExpenseStateMachine.cs#L51-L54).

**Persistence & side effects:** none new on submit (the expense already exists); status and
`History` mutate in place in `DataStore.Expenses`.

**Tests:** None found.

**Juniors miss:** `RefreshPolicy` runs *again* at submit, not just at create — so a policy
that became blocking after creation (e.g. a category limit lowered) is caught. **Seniors
notice:** the policy check and the transition are not atomic; two concurrent submits on the
same expense could both pass the check (a TOCTOU window). Acceptable for a single-user demo;
flagged in the [risk register](../08-reference/risk-register.md).

**Drill:** Create an expense for $30 with no receipt and try to submit. Which flag blocks
it, and what HTTP status + body does the client get? Trace the error from
[`PolicyEngine` L50](../../../backend/Services/PolicyEngine.cs#L50) to the `details` array in
[middleware L57-58](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L57-L58) to
[`client.js` L42](../../../frontend/src/api/client.js#L42).
**Self-grade:** Basic = named the flag. Solid = traced to 422 + details. Strong = showed where the UI renders `details`.

---

## Flow 3: Receipt upload → background OCR

**Why it matters:** the only file-upload path and one of two background flows. Shows the
hand-off from a synchronous request to async processing.

**Open first:** [`ReceiptService.cs`](../../../backend/Services/ReceiptService.cs) and
[`ReceiptStorage.cs`](../../../backend/Services/ReceiptStorage.cs).

**Trace:**

| Step | Owner | File | What happens | Data shape | Risk |
| --- | --- | --- | --- | --- | --- |
| 1 | Client | [`ExpenseDetailPage.handleUpload` L55-62](../../../frontend/src/pages/ExpenseDetailPage.jsx#L55-L62) | `FormData` POST to `/api/expenses/{id}/receipt` | multipart | — |
| 2 | API | [`ReceiptsController.Upload` L23-33](../../../backend/Controllers/ReceiptsController.cs#L23-L33) | Size-limit attribute; calls service | `IFormFile` | — |
| 3 | Service | [`ReceiptService.UploadAsync` L30-49](../../../backend/Services/ReceiptService.cs#L30-L49) | Ownership + editable checks, save, attach | `Receipt` | — |
| 4 | Service | [`ReceiptStorage.SaveAsync` L37-72](../../../backend/Services/ReceiptStorage.cs#L37-L72) | Validate type/size/ext; write with GUID name | file on disk | path traversal (defended) |
| 5 | Service | [`UploadAsync` L52-58](../../../backend/Services/ReceiptService.cs#L52-L58) | Enqueue OCR job + policy refresh | delegate | — |
| 6 | Worker | [`QueuedHostedService` L40-48](../../../backend/Background/QueuedHostedService.cs#L40-L48) | Dequeues, runs job in a fresh scope | — | — |
| 7 | Worker | [`BackgroundJobs.ProcessReceipt` L16-33](../../../backend/Background/BackgroundJobs.cs#L16-L33) | Delay, set `ExtractedAmount`, status=Processed | `Receipt` | — |
| 8 | Client | [`ExpenseDetailPage` L26-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L26-L32) | Polls every 1.5s while `Pending` | — | poll, not push |

**Validation & authorization:** allowlist + size + extension cross-check in
[`SaveAsync` L39-54](../../../backend/Services/ReceiptStorage.cs#L39-L54); only the owner of an
editable expense can upload ([`UploadAsync` L35-38](../../../backend/Services/ReceiptService.cs#L35-L38)).

**Persistence & side effects:** a file is written to `uploads/`; a `Receipt` is added to the
store; the expense's `ReceiptId` is set.

**Tests:** None found.

**Juniors miss:** the stored filename is a server GUID, never the user's filename — that's
the path-traversal defense ([`SaveAsync` L56](../../../backend/Services/ReceiptStorage.cs#L56)).
**Seniors notice:** content type is taken from `file.ContentType` (client-supplied) and the
file bytes are never sniffed — a `.pdf` could contain anything; *possible risk*, noted in the
[security checklist](../05-quality-engineering/05-security-checklist.md).

**Drill:** What happens if the OCR job throws? Find where the exception is caught and why
the worker survives ([`QueuedHostedService` L45-48](../../../backend/Background/QueuedHostedService.cs#L45-L48)).
**Self-grade:** Basic = found the try/catch. Solid = explained one bad job doesn't kill the loop. Strong = noted the receipt is then stuck `Pending` forever with no retry.

---

## Flow 4: Manager approval (the authorization boundary)

**Why it matters:** the clearest example of *resource-scoped* authorization — not "is this a
manager" but "is this manager *this employee's* manager."

**Trace:**

| Step | Owner | File | What happens | Risk |
| --- | --- | --- | --- | --- |
| 1 | API | [`ExpensesController.Approve` L79-86](../../../backend/Controllers/ExpensesController.cs#L79-L86) | `[Authorize(Roles="Manager")]` gate, then service | coarse gate only |
| 2 | Service | [`Approve` L132-137](../../../backend/Services/ExpenseService.cs#L132-L137) | `GetForActor` (can-view) then `EnsureManagesOwner` | — |
| 3 | Service | [`EnsureManagesOwner` L198-202](../../../backend/Services/ExpenseService.cs#L198-L202) | Throws 403 unless actor manages the owner | — |
| 4 | Service | [`Transition` L169-178](../../../backend/Services/ExpenseService.cs#L169-L178) | `Submitted → Approved` allowed for Manager | — |

**Why two layers?** The `[Authorize(Roles="Manager")]` attribute is a *coarse* gate (any
manager). The real check — *this* manager manages *this* employee — is in the service. This
is the defense against **IDOR** (insecure direct object reference): a manager guessing
another team's expense id is rejected at [L200](../../../backend/Services/ExpenseService.cs#L200).

**Tests:** None found. **This is the highest-value thing to test first** — see
[05/02 recipe 4](../05-quality-engineering/02-writing-tests-here.md).

**Juniors miss:** that `[Authorize(Roles="Manager")]` alone would be an IDOR hole.
**Seniors notice:** the rule is "direct report only" — there's no transitive hierarchy or
delegation; a skip-level manager can't approve. That's a product decision worth surfacing.

**Drill:** Write the manual reproduction for the IDOR attempt: log in as `manager@demo.com`,
get an expense id belonging to an employee who reports to a *different* manager (you'll need
to add one to the seeder), and `POST …/approve`. What status do you get and from which line?
**Self-grade:** Basic = predicted 403. Solid = pointed at L200. Strong = explained why the attribute wasn't enough.

---

## Flow 5: Reimbursement (a system state transition)

**Why it matters:** the only transition with **no human actor**, and the second background
flow. Shows the distinction between role-gated and system transitions.

**Trace:**

| Step | Owner | File | What happens | Risk |
| --- | --- | --- | --- | --- |
| 1 | API | [`ExpensesController.Reimburse` L97-104](../../../backend/Controllers/ExpensesController.cs#L97-L104) | `[Authorize(Roles="Finance")]`, calls service | — |
| 2 | Service | [`StartReimbursement` L150-156](../../../backend/Services/ExpenseService.cs#L150-L156) | `Approved → Reimbursing` (Finance), enqueue job | fire-and-forget enqueue |
| 3 | Worker | [`ProcessReimbursement` L40-69](../../../backend/Background/BackgroundJobs.cs#L40-L69) | Delay, check `CanSystemTransition`, set Reimbursed + payment ref | — |
| 4 | Client | [`ExpenseDetailPage` L26-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L26-L32) | Polls while status is `Reimbursing` | — |

**Note the two-step status:** `Approved → Reimbursing` happens synchronously (so the UI
updates instantly and the action can't be triggered twice), then `Reimbursing → Reimbursed`
happens in the worker. The worker uses `CanSystemTransition`
([L51](../../../backend/Background/BackgroundJobs.cs#L51)) which checks the edge is legal but
*skips* the role check — because "the system" has no role.

**Tests:** None found.

**Juniors miss:** why split into two statuses instead of going straight to `Reimbursed`. The
answer: idempotency + UX — the intermediate state prevents a double-reimburse and tells the
user something is in flight. **Seniors notice:** the enqueue at
[`StartReimbursement` L154](../../../backend/Services/ExpenseService.cs#L154) is **not
awaited** (a discarded `ValueTask`) — and if the process crashes between the status change
and the job running, the expense is stuck in `Reimbursing` with no payment and no retry.
That's the textbook argument for an **outbox pattern** (see [03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md)).

**Drill:** Identify every place "stuck in `Reimbursing`" could happen and what would recover
it today (answer: nothing recovers it today).
**Self-grade:** Basic = found the two-step. Solid = explained idempotency. Strong = articulated the crash window + named the outbox fix.

---

## Flow 6: Role-scoped reporting

**Why it matters:** shows reuse of authorization logic — the report doesn't reimplement
"who can see what," it calls the same `List`.

**Trace:**

| Step | Owner | File | What happens |
| --- | --- | --- | --- |
| 1 | API | [`ReportsController.Summary`](../../../backend/Controllers/ReportsController.cs) | Resolves actor, calls service |
| 2 | Service | [`ReportingService.Summarize`](../../../backend/Services/ReportingService.cs) | Calls `ExpenseService.List(actor, …)` then aggregates |
| 3 | Service | [`ExpenseService.List` L30-52](../../../backend/Services/ExpenseService.cs#L30-L52) | Role-scoped visibility (own / team / all) |

**Seniors notice:** because the report is built on `List`, an employee's report can never
include a teammate's spend — visibility is enforced in exactly one place. This is the payoff
of putting authorization in the service layer. If reporting had its own query, the two could
drift and leak data.

**Drill:** A PR adds a "company-wide totals" widget by querying `DataStore.Expenses`
directly in the reporting service. What's the review objection, in one sentence? (Answer:
it bypasses role scoping — a data-leak boundary violation.)
**Self-grade:** Basic = "don't query the store directly." Solid = named the leaked boundary. Strong = proposed the fix (a Finance-only path through `List`).
