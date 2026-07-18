# Validation, Auth & Permissions

## Two kinds of "is this allowed?"

| Question | Mechanism | Where | HTTP on failure |
| --- | --- | --- | --- |
| Is the input well-formed? | DataAnnotations + `[ApiController]` | [`Dtos.cs`](../../../backend/Dtos/Dtos.cs) | 400 |
| Does it satisfy business rules? | `PolicyEngine` | [`PolicyEngine.Evaluate`](../../../backend/Services/PolicyEngine.cs#L34-L66) | 422 |
| Are you logged in? | `[Authorize]` + auth handler | [`TokenAuthenticationHandler`](../../../backend/Security/TokenAuthenticationHandler.cs) | 401 |
| Do you have the role? | `[Authorize(Roles=…)]` | controller actions | 403 |
| May you touch *this* resource? | service checks | `ExpenseService` | 403 |

Understanding which layer answers which question is the core of this page.

## Authentication: token → identity

1. Login verifies the PBKDF2 hash and creates a `Session`
   ([`AuthService.Login` L24-36](../../../backend/Security/AuthService.cs#L24-L36)).
2. Each request, the handler reads the bearer token, validates the session, and builds a
   `ClaimsPrincipal` with a **role claim**
   ([`TokenAuthenticationHandler` L44-55](../../../backend/Security/TokenAuthenticationHandler.cs#L44-L55)).
3. `[Authorize(Roles="Manager")]` matches that role claim.
4. `UserContext.RequireUser()` turns the claim's user-id back into the `User` entity for the
   service layer ([`UserContext` L35-40](../../../backend/Security/UserContext.cs#L35-L40)).

**Password security done right (study this):** PBKDF2 with a per-user random salt, 100k
iterations, and a **constant-time** comparison
([`PasswordHasher`](../../../backend/Security/PasswordHasher.cs)). The login path verifies
the hash even though it short-circuits on a missing user — the comment at
[`AuthService` L27-28](../../../backend/Security/AuthService.cs#L27-L28) explains the
timing-attack reasoning (though note: it *does* return early when the user is null, so the
timing equalization is partial — *investigate* if you care about that threat).

## Authorization: the two-layer model (and IDOR)

This is the most important security idea in the repo.

- **Coarse (attribute):** `[Authorize(Roles="Manager")]` on
  [`Approve` L80](../../../backend/Controllers/ExpensesController.cs#L80) means "some
  manager." Necessary but **not sufficient**.
- **Fine (resource):** `EnsureManagesOwner`
  ([`ExpenseService` L198-202](../../../backend/Services/ExpenseService.cs#L198-L202)) means
  "*this* manager manages *this* expense's owner." This is the **IDOR** defense — without it,
  any manager could approve any team's expense by guessing an id.

Visibility follows the same shape — `CanView` / `List` scope reads to
own/team/all by role ([`ExpenseService` L30-52, L190-196](../../../backend/Services/ExpenseService.cs#L30-L52)),
and writes are owner-scoped via `GetOwned`
([L181-188](../../../backend/Services/ExpenseService.cs#L181-L188)).

**The default-deny habit:** every role `switch` has a `_ => false` / `Enumerable.Empty`
arm. A new role added to the enum gets *no* access until someone deliberately grants it.
Fail closed.

## The policy engine

[`PolicyEngine.Evaluate`](../../../backend/Services/PolicyEngine.cs#L34-L66) returns flags
with `Blocking` or `Warning` severity. Blocking flags stop submission
([`Submit` L119-126](../../../backend/Services/ExpenseService.cs#L119-L126)); warnings ride
along to the approver. Rules: amount > 0, description present, no future date, stale-date
warning, receipt-required over $25, per-category limits, high-value warning.

**Re-evaluation matters:** policy is recomputed at create, update, *and* submit
([`RefreshPolicy` L159-164](../../../backend/Services/ExpenseService.cs#L159-L164)) — so a
flag can appear at submit time that wasn't there at create time.

## Junior misses vs. senior checks

| Junior | Senior |
| --- | --- |
| Trusts `[Authorize(Roles=…)]` alone | Asks "can user A act on user B's object?" — checks for IDOR |
| Validates shape, forgets business rules | Knows 400 (shape) and 422 (rules) are different layers |
| Returns the entity | Returns a DTO; checks nothing sensitive leaks |
| Adds a role to the enum | Audits every `switch` for the new arm and confirms default-deny |
| Stores password as-is or with MD5 | Recognizes PBKDF2 + salt + constant-time compare |

## Drills

1. **IDOR hunt:** add a second manager + their report to the seeder, then try to approve
   across teams. Confirm the 403 comes from
   [`EnsureManagesOwner` L200](../../../backend/Services/ExpenseService.cs#L200), not the
   attribute.
2. **Leak hunt:** grep the DTOs for any field that mirrors a sensitive entity field. (You
   won't find `PasswordHash` — confirm *why* via `Mappings`.)
3. **Fail-closed:** mentally add `UserRole.Auditor` to the enum. List every place you'd have
   to touch for it to see anything. (That list *is* the security surface.)
