# Security Checklist

Each item is tied to real code, with a verdict: ✅ handled · ⚠️ partial / context-dependent ·
🔲 not applicable yet (but will be).

## Authentication & sessions
- ✅ **Password storage:** PBKDF2 + per-user salt + constant-time compare — [`PasswordHasher`](../../../backend/Security/PasswordHasher.cs).
- ✅ **Tokens are random, 256-bit** — [`AuthService.GenerateToken` L55-56](../../../backend/Security/AuthService.cs#L55-L56).
- ⚠️ **Token storage (client):** `localStorage` is readable by any XSS — [`client.js` L6-10](../../../frontend/src/api/client.js#L6-L10). Acceptable for a demo; httpOnly cookies are stronger.
- ⚠️ **Session expiry:** enforced on access, pruned lazily — [`AuthService` L46-49](../../../backend/Security/AuthService.cs#L46-L49). No idle-timeout sweep; slow memory growth.
- ⚠️ **Timing:** login returns early on unknown user — [`AuthService` L29](../../../backend/Security/AuthService.cs#L29) — so the constant-time compare only fully applies when the user exists. Minor.

## Authorization
- ✅ **Role gate + resource scope (anti-IDOR):** [`EnsureManagesOwner`](../../../backend/Services/ExpenseService.cs#L198-L202), [`GetOwned`](../../../backend/Services/ExpenseService.cs#L181-L188), [`CanView`](../../../backend/Services/ExpenseService.cs#L190-L196).
- ✅ **Fail-closed defaults:** `_ => false` arms in every role switch.
- ✅ **File access reuses expense authz:** [`ReceiptService.GetForActor` L63-70](../../../backend/Services/ReceiptService.cs#L63-L70).
- ✅ **UI capability list is not the gate:** server re-checks every action.

## Input validation
- ✅ **Shape validation** (DataAnnotations, →400) + **business rules** (`PolicyEngine`, →422).
- ✅ **Enum/DateOnly binding** rejects garbage automatically.

## File uploads
- ✅ **Allowlist content type + size cap + extension cross-check** — [`SaveAsync` L39-54](../../../backend/Services/ReceiptStorage.cs#L39-L54).
- ✅ **No path traversal:** server-GUID names + stored outside wwwroot + `OpenRead` guard — [L56, L75-82](../../../backend/Services/ReceiptStorage.cs#L75-L82).
- ⚠️ **No byte/content sniffing or AV:** content type is trusted from the client header — [L45](../../../backend/Services/ReceiptStorage.cs#L45). A `.pdf` could be anything. *Possible risk.*

## Injection / XSS / CSRF
- ✅ **SQL injection:** N/A — no SQL (in-memory). Will matter after an EF Core migration (use parameterized queries / LINQ, which EF does by default).
- ✅ **XSS:** React escapes by default; no `dangerouslySetInnerHTML` in the codebase.
- ⚠️ **CSRF:** bearer-token-in-header (not cookies) is not CSRF-prone; if you switch to cookie auth, add anti-forgery.

## Transport / config / secrets
- 🔲 **HTTPS:** dev is HTTP ([launchSettings](../../../backend/Properties/launchSettings.json)); enforce TLS in prod.
- ✅ **No secrets in repo** today; 🔲 when a DB/payment key is added, use user-secrets/env/vault, not `appsettings.json`.
- ⚠️ **CORS fallback** silently defaults to localhost on a config typo — [`Program.cs` L37-38](../../../backend/Program.cs#L37-L38).

## Availability
- 🔲 **Rate limiting:** none. Login is brute-forceable (slowed only by PBKDF2 cost). Add throttling for a real deployment.
- ⚠️ **Request size:** receipt upload capped via `[RequestSizeLimit]` — [`ReceiptsController` L24](../../../backend/Controllers/ReceiptsController.cs#L24) — but other endpoints use defaults.

## Error handling
- ✅ **No internal leakage:** 500s return a generic message; full detail only logged — [middleware L45-54](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L45-L54).
- ✅ **No credential logging** (verify this stays true — see [Kata 7](../04-code-reading-gym/04-review-katas.md)).

---

## Pre-merge security checklist (copy into PRs that touch these areas)
- [ ] New endpoint: does it check **resource** ownership, not just role?
- [ ] New response: does the DTO exclude every sensitive field?
- [ ] New enum value/role: is every authorization `switch` updated and still fail-closed?
- [ ] User-supplied path/filename: never used to build a filesystem path?
- [ ] New external call / secret: configured via env/secret store, not committed?
- [ ] New logging: no tokens/passwords/PII?
- [ ] State change: goes through `Transition` / the service, not direct mutation?
