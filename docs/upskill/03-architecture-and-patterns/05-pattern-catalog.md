# Pattern Catalog

17 patterns this repo uses, each with a real anchor, why it works, when it bites, and a
drill. Learn to recognize these and you'll spot them (and their absence) everywhere.

---

## Pattern: Centralized State Machine
**Problem:** entity lifecycle rules scatter across handlers and drift.
**General shape:** one table of legal `(from → to)` edges; all changes funnel through one method.
**Real example:** [`ExpenseStateMachine` L19-58](../../../backend/Domain/ExpenseStateMachine.cs#L19-L58); funnel at [`ExpenseService.Transition` L169-178](../../../backend/Services/ExpenseService.cs#L169-L178).
**Second:** the system-only variant `CanSystemTransition` [L57-58](../../../backend/Domain/ExpenseStateMachine.cs#L57-L58).
**Why it works:** illegal transitions are impossible regardless of entry point; the lifecycle is readable in one place.
**Failure modes:** if someone sets `expense.Status = X` directly (bypassing `Transition`), the guarantee is gone. The table encodes role too, coupling lifecycle and authz — fine here, awkward if rules diverge.
**Use when:** any entity with a non-trivial lifecycle. **Avoid when:** two states with a single transition (overkill).
**Drill:** add a `Cancelled` state reachable from `Draft`/`Rejected`. How many files change? (Enum + table + maybe UI.)

---

## Pattern: Thin Controller / Fat Service
**Problem:** business logic in controllers can't be reused or tested without HTTP.
**General shape:** controller binds + delegates; service holds rules.
**Real example:** [`ExpensesController.Submit` L71-77](../../../backend/Controllers/ExpensesController.cs#L71-L77) vs [`ExpenseService.Submit` L114-130](../../../backend/Services/ExpenseService.cs#L114-L130).
**Why it works:** rules are unit-testable; a second entry point (CLI/worker) reuses the service.
**Failure modes:** "anemic" controllers can hide that *no one* did authorization if the service forgets it. Discipline required.
**Drill:** could `Approve` be invoked by a scheduled job without a controller? Trace what you'd need.

---

## Pattern: DTO ↔ Entity Mapping (anti-corruption boundary)
**Problem:** returning entities leaks internals and couples the wire to the DB.
**Real example:** [`Mappings.cs`](../../../backend/Services/Mappings.cs); `User` has `PasswordHash`, `UserResponse` does not.
**Why it works:** the contract evolves independently of the model; secrets can't leak by accident.
**Failure modes:** mapping drift (a new entity field silently missing from the DTO) — usually fine, occasionally a bug.
**Drill:** [03/01 drill](01-boundaries-and-layers.md) — add an entity field, confirm it's invisible until mapped.

---

## Pattern: Service-layer Authorization (resource scoping)
**Problem:** role checks alone allow IDOR (acting on someone else's object).
**Real example:** `GetOwned`, `CanView`, `EnsureManagesOwner` [L181-205](../../../backend/Services/ExpenseService.cs#L181-L205).
**Why it works:** the "may you touch *this* object" check lives next to the action, in one layer.
**Failure modes:** if a new endpoint forgets to call it, the hole is silent. Tests are the safety net (none exist yet — see [05](../05-quality-engineering/)).
**Drill:** which method protects reads vs writes? (Reads: `GetForActor`/`CanView`. Writes: `GetOwned`.)

---

## Pattern: Two-layer Validation
**Problem:** shape validation and business validation get conflated.
**Real example:** DataAnnotations in [`Dtos.cs`](../../../backend/Dtos/Dtos.cs) (→400) vs `PolicyEngine` (→422).
**Why it works:** cheap shape checks at the edge fail fast; rich rules live in the domain and produce actionable messages.
**Failure modes:** duplicating a rule in both layers; or assuming DataAnnotations cover business rules.
**Drill:** which layer rejects `amount = -5`? (Both: `[Range]` 400 first; `PolicyEngine` AMOUNT_INVALID would 422 if it got through.)

---

## Pattern: Policy/Rule Collection with Severity
**Problem:** business rules as nested `if`s become unreadable and untestable.
**Real example:** [`PolicyEngine.Evaluate` L34-66](../../../backend/Services/PolicyEngine.cs#L34-L66) returns a list of `PolicyFlag(Code, Message, Severity)`.
**Why it works:** rules are additive and independently testable; severity separates "block" from "warn"; codes are stable for clients.
**Failure modes:** order-dependence between rules; rules needing external data (here `hasProcessedReceipt` is passed in to keep `Evaluate` pure).
**Drill:** add a "weekend expense" warning. One new `if`, one code — note how isolated it is.

---

## Pattern: Exception → ProblemDetails Middleware
**Problem:** try/catch in every controller is noise and inconsistent.
**Real example:** [`ExceptionHandlingMiddleware` L35-63](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L35-L63) maps exception type → status.
**Why it works:** services `throw` domain exceptions; one place renders them; controllers stay clean.
**Failure modes:** swallowing the real error (500s log full detail at [L45-46](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L45-L46) but hide it from the client — correct); a new exception type with no arm falls to 500.
**Drill:** add a `ConflictException` → 409. Where do you wire it? (One `switch` arm + the exception class.)

---

## Pattern: Custom AuthenticationHandler
**Problem:** you need auth but don't want a heavy library (learning goal here).
**Real example:** [`TokenAuthenticationHandler` L14-56](../../../backend/Security/TokenAuthenticationHandler.cs#L14-L56) implements the framework's handler contract.
**Why it works:** integrates with `[Authorize]`/roles using only the built-in abstraction; swappable for JWT later with the same surface.
**Failure modes:** opaque tokens require server-side session lookup (state); no signature, no expiry-in-token. Fine for a demo, a scaling concern in prod.
**Drill:** what would change to swap to JWT? (The handler validates a signature instead of a session; `AuthService` issues signed tokens; `DataStore.Sessions` can go.)

---

## Pattern: In-memory Repository Singleton
**Problem:** you want persistence semantics without a DB (learning).
**Real example:** [`DataStore.cs`](../../../backend/Storage/DataStore.cs) as a singleton.
**Why it works:** dead simple; isolates persistence so a real DB is a localized swap.
**Failure modes:** no durability, no transactions, no isolation; singleton + mutable entities = data races (see [risk register](../08-reference/risk-register.md)).
**Drill:** list every behavior you'd lose/gain swapping to EF Core (see [03/02](02-data-model-and-persistence.md)).

---

## Pattern: Background Queue (Channel) + Hosted Consumer
**Problem:** do slow work without blocking the request.
**Real example:** [`BackgroundTaskQueue`](../../../backend/Background/BackgroundTaskQueue.cs) + [`QueuedHostedService`](../../../backend/Background/QueuedHostedService.cs).
**Why it works:** decouples request latency from work duration; bounded channel gives backpressure.
**Failure modes:** in-process + in-memory = lost on crash; no retries. See [03/04](04-side-effects-async-and-reliability.md).
**Drill:** what's the queue capacity and what happens when it's full? ([L19-25](../../../backend/Background/BackgroundTaskQueue.cs#L19-L25)).

---

## Pattern: Scope-per-Job (DI scope in background work)
**Problem:** a singleton worker can't safely use scoped services.
**Real example:** `using var scope = _services.CreateScope()` [L42-43](../../../backend/Background/QueuedHostedService.cs#L42-L43); job resolves from the scope [`ReceiptService` L55-56](../../../backend/Services/ReceiptService.cs#L55-L56).
**Why it works:** each job gets request-like isolation; prevents captive dependencies; ready for scoped `DbContext`.
**Failure modes:** capturing a scoped service in the delegate closure instead of resolving from `sp` re-introduces the bug.
**Drill:** spot where the job resolves vs captures. Why does it pass `sp` rather than close over `_expenses`?

---

## Pattern: Idempotent Side Effect via Guard
**Problem:** a side effect that runs twice causes double-payment/double-email.
**Real example:** [`ProcessReimbursement` L51-56](../../../backend/Background/BackgroundJobs.cs#L51-L56) re-checks the transition before acting.
**Why it works:** re-running is a no-op once the terminal state is reached.
**Failure modes:** `ProcessReceipt` lacks the guard (harmless here) — inconsistent idempotency is a smell.
**Drill:** make `ProcessReceipt` idempotent. What guard? (e.g. skip if status already `Processed`.)

---

## Pattern: Capability List for the Client (`availableActions`)
**Problem:** the UI shouldn't hardcode "who can do what" — it'll drift from the server.
**Real example:** [`Mappings.AvailableActions` L45-72](../../../backend/Services/Mappings.cs#L45-L72) → consumed by `can(...)` [`ExpenseDetailPage` L47](../../../frontend/src/pages/ExpenseDetailPage.jsx#L47).
**Why it works:** the server computes affordances once; the UI just renders them; rules stay server-side.
**Failure modes:** it's UX, **not** security — the server still re-checks on the actual call. Treating the list as the gate would be a vulnerability.
**Drill:** remove a button by removing its action from the list — confirm the endpoint still rejects a hand-crafted request.

---

## Pattern: Null-coalescing-throw for Existence
**Problem:** "fetch or 404" written as verbose if/return everywhere.
**Real example:** `?? throw new NotFoundException(...)` [`ExpenseService` L56-57](../../../backend/Services/ExpenseService.cs#L56-L57).
**Why it works:** one expression; the non-null result flows on; the middleware turns it into 404.
**Failure modes:** throwing for *expected* control flow can be costly in hot loops (not an issue here).
**Drill:** rewrite one as explicit if/return and judge readability.

---

## Pattern: Default-Deny Switch
**Problem:** adding an enum value silently grants access.
**Real example:** `_ => false` / `Enumerable.Empty` arms in `CanView`/`List` [L190-196, L43](../../../backend/Services/ExpenseService.cs#L190-L196).
**Why it works:** new roles get nothing until explicitly handled — fail closed.
**Failure modes:** a non-exhaustive switch that defaults *open* is the inverse bug — always check the default arm in reviews.
**Drill:** see [03/03 drill 3](03-validation-auth-and-permissions.md).

---

## Pattern: API Client with Normalized Errors (frontend)
**Problem:** scattered `fetch` calls duplicate token/JSON/error handling.
**Real example:** [`client.js` L20-53](../../../frontend/src/api/client.js#L20-L53) — one `request`, an `ApiError` carrying status + `details`.
**Why it works:** every call gets the token and a consistent error shape; the 204 case is handled once ([L35](../../../frontend/src/api/client.js#L35)).
**Failure modes:** token in `localStorage` is XSS-readable; the wrapper assumes JSON bodies.
**Drill:** trace how a 422 `details` array reaches the form UI ([client L42](../../../frontend/src/api/client.js#L42) → [`ExpenseFormPage`](../../../frontend/src/pages/ExpenseFormPage.jsx)).

---

## Pattern: Stable Effect Deps via `useCallback` + active-flag
**Problem:** effects re-firing every render; stale async responses overwriting fresh state.
**Real example:** `useCallback(load, [id])` + cleanup [`ExpenseDetailPage` L15-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L15-L32); `active` flag [`ExpensesPage` L13-25](../../../frontend/src/pages/ExpensesPage.jsx#L13-L25).
**Why it works:** stable identity prevents infinite loops; the flag discards late responses.
**Failure modes:** forgetting either → fetch loop or out-of-order results.
**Drill:** see [02/02 React drill](../02-stack-and-language-mastery/02-framework-mental-models.md).
