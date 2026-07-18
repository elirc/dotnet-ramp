# Annotation Drills

For each: open the anchored code and write, in your own words — **inputs**, **outputs**,
**dependencies**, **invariants** (what must always be true), **side effects**, **failure
modes**. Then check against the notes.

**Self-grading rubric** (apply to every drill):
- **Basic:** named inputs/outputs correctly.
- **Solid:** also identified side effects + at least one failure mode.
- **Strong:** also stated an invariant and what *breaks* if it's violated.

---

### Drill 1 — `ExpenseService.Transition`
[L169-178](../../../backend/Services/ExpenseService.cs#L169-L178)
*Notes:* Invariant = status only ever changes here, and only along a legal edge. Side effect
= mutates `Status`, `UpdatedAt`, appends `History`. Failure = throws `DomainException` if the
edge is illegal for the role. Break the invariant (set status elsewhere) and the audit trail
+ legality guarantee collapse.

### Drill 2 — `PolicyEngine.Evaluate`
[L34-66](../../../backend/Services/PolicyEngine.cs#L34-L66)
*Notes:* Pure function of `(expense, hasProcessedReceipt)` → flags; no side effects (good for
testing). Invariant = AMOUNT_INVALID/DESCRIPTION_REQUIRED/DATE_FUTURE are Blocking. Failure
mode = none thrown; it *reports*, the caller decides.

### Drill 3 — `ReceiptStorage.SaveAsync`
[L37-72](../../../backend/Services/ReceiptStorage.cs#L37-L72)
*Notes:* Inputs = expenseId, IFormFile, ct. Side effect = writes a file. Invariants = stored
name is a server GUID (never user input); size ≤ cap; content type in allowlist. Failure =
`DomainException` on empty/oversize/wrong-type/mismatched-ext. Break the GUID-name invariant
→ path traversal.

### Drill 4 — `TokenAuthenticationHandler.HandleAuthenticateAsync`
[L30-55](../../../backend/Security/TokenAuthenticationHandler.cs#L30-L55)
*Notes:* Output = `AuthenticateResult` (NoResult / Fail / Success). Dependency = `AuthService`.
Invariant = a successful ticket always carries a role claim. Failure = no header → NoResult
(anonymous allowed through to `[Authorize]` which then 401s); bad token → Fail.

### Drill 5 — `QueuedHostedService.ExecuteAsync`
[L24-52](../../../backend/Background/QueuedHostedService.cs#L24-L52)
*Notes:* Invariant = one failing job never stops the loop (try/catch around the job, not the
dequeue). Side effect = runs arbitrary work in a fresh scope. Failure = on cancellation,
breaks cleanly. Subtle: an exception in `DequeueAsync` other than cancellation would escape —
*investigate* whether that can happen.

### Drill 6 — `ExpenseService.List`
[L30-52](../../../backend/Services/ExpenseService.cs#L30-L52)
*Notes:* Invariant = visibility scoped by role *before* filters; default arm returns empty
(fail closed). Output = materialized, sorted list. Failure mode for a new role = no results
(safe). Note LINQ deferral until `ToList`.

### Drill 7 — `Mappings.AvailableActions`
[L45-72](../../../backend/Services/Mappings.cs#L45-L72)
*Notes:* Pure-ish (reads store). Output = list of capability strings. Invariant = it must
mirror the service's real checks; if they diverge, the UI lies (but the server still
enforces). Failure mode = none thrown.

### Drill 8 — `AuthService.Login`
[L24-36](../../../backend/Security/AuthService.cs#L24-L36)
*Notes:* Output = `(user, token)?` — null on failure. Side effect = writes a `Session`.
Invariant = token maps to exactly one user/session. Failure = wrong password/user → null →
401 at the controller.

### Drill 9 — `ReceiptService.UploadAsync`
[L30-61](../../../backend/Services/ReceiptService.cs#L30-L61)
*Notes:* Orchestrates 4 things: authorize, store, attach, enqueue. Invariant = only an owner
of an *editable* expense can upload; old receipt is removed. Side effects = file + store +
queue. Failure = throws before enqueue if checks fail (so no orphaned job).

### Drill 10 — `ExpenseDetailPage` polling effect
[L26-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L26-L32)
*Notes:* Inputs = `expense`, `load`. Side effect = schedules a re-fetch timer. Invariant =
cleanup clears the timer (no leak / no double-poll). Failure mode = if status never leaves
`Pending`/`Reimbursing`, it polls forever (the background job is expected to end it).
