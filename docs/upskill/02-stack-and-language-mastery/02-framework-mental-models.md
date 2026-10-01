# Framework Mental Models

## ASP.NET Core: the middleware pipeline

**Concept:** a request passes through an ordered chain of middleware, each able to act
before and after the next. Order is significant.

**Real code:** [`Program.cs` L51-56](../../../backend/Program.cs#L51-L56):

```
ExceptionHandling → CORS → Authentication → Authorization → MapControllers
```

**Why this order:**
- Exception handling is **first** so it wraps everything after it — any thrown
  `DomainException` deeper in the stack is caught and turned into `ProblemDetails`
  ([middleware L23-33](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L23-L33)).
- Authentication must run **before** Authorization — you can't check roles before you know
  who the user is. The handler that populates `HttpContext.User` is
  [`TokenAuthenticationHandler`](../../../backend/Security/TokenAuthenticationHandler.cs).

**Failure mode:** put `UseAuthorization` before `UseAuthentication` and every `[Authorize]`
fails because there's no identity yet. Put exception handling *after* the controllers and
unhandled exceptions escape as raw 500s. **Drill:** what breaks if you move
`UseMiddleware<ExceptionHandlingMiddleware>` to after `MapControllers`?

## Dependency Injection & lifetimes

**Concept:** three lifetimes — **Singleton** (one for the app), **Scoped** (one per
request), **Transient** (one per resolution).

**Real code:** [`Program.cs` L16-26](../../../backend/Program.cs#L16-L26):

| Service | Lifetime | Why |
| --- | --- | --- |
| `DataStore` | Singleton | It *is* the database; shared by all requests + the worker |
| `IBackgroundTaskQueue` | Singleton | Producers (requests) and consumer (worker) must share one queue |
| `PolicyEngine`, `AuthService`, `ReceiptStorage` | Singleton | Stateless (or config-only) |
| `ExpenseService`, `ReceiptService`, `ReportingService` | Scoped | Per-request use cases |
| `IUserContext` | Scoped | Wraps the per-request `HttpContext` |

**The captive-dependency bug (the classic interview question):** a **singleton** must never
depend on a **scoped** service — the singleton would capture the first request's scope and
reuse it forever. Check this repo: the singletons (`DataStore`, `AuthService`,
`ReceiptStorage`, the queue) depend only on `IConfiguration`/`IWebHostEnvironment` (also
singletons) — no captive dependency. ✅

**The subtle part — background work and scopes:** `QueuedHostedService` is effectively a
singleton, so it must **not** inject `ExpenseService` (scoped) directly. Instead it injects
the **root** `IServiceProvider` and creates a fresh scope per job
([L42-43](../../../backend/Background/QueuedHostedService.cs#L42-L43)), then the job resolves
scoped services from *that* scope ([`ReceiptService` L55](../../../backend/Services/ReceiptService.cs#L55)).
This is the correct, idiomatic pattern — study it; it comes up constantly.

**Drill:** What would go wrong if `QueuedHostedService` took `ExpenseService` as a
constructor parameter? (Answer: the DI container throws at startup — you can't inject a
scoped service into a singleton — which is the framework saving you from the bug.)

## Model binding & validation

**Concept:** ASP.NET binds the request (route, query, body, form) to action parameters, then
`[ApiController]` auto-validates DataAnnotations and returns 400 before your code runs.

**Real code:**
- Query binding incl. enums and `DateOnly`:
  [`ExpensesController.List` L28-32](../../../backend/Controllers/ExpensesController.cs#L28-L32).
- Body binding to a record: [`CreateExpenseRequest`](../../../backend/Dtos/Dtos.cs) with
  `[Required]`, `[Range]`, `[MaxLength]`.
- Form/file binding: `IFormFile file` at
  [`ReceiptsController` L25](../../../backend/Controllers/ReceiptsController.cs#L25).

**Two-layer validation — know the difference:** DataAnnotations check *shape* ("amount is
between 0.01 and 1,000,000") at the edge; the `PolicyEngine` checks *business rules*
("receipt required over $25") in the domain. The first returns 400 automatically; the second
returns 422 via the middleware. **Failure mode:** relying only on DataAnnotations would let
business-invalid-but-shape-valid data through.

## React: the render model

**Concept:** React re-renders a component when its state or props change; effects run *after*
render; cleanup runs before the next effect or unmount.

**Real code:**
- State + effect fetch:
  [`ExpenseDetailPage` L10-23](../../../frontend/src/pages/ExpenseDetailPage.jsx#L10-L23).
- **`useCallback` to stabilize a function identity** so the effect's dependency array
  doesn't re-fire every render: `load` at
  [L15-21](../../../frontend/src/pages/ExpenseDetailPage.jsx#L15-L21).
- **Effect cleanup** clearing a timer:
  [L26-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L26-L32).
- **Avoiding a stale-closure bug on async fetch** with an `active` flag:
  [`ExpensesPage` L13-25](../../../frontend/src/pages/ExpensesPage.jsx#L13-L25) and
  [`AuthContext` L14-31](../../../frontend/src/auth/AuthContext.jsx#L14-L31).

**Why the `active` flag matters:** if the component unmounts (or deps change) before the
fetch resolves, calling `setState` on an unmounted component is wasted work / a warning. The
flag makes the late response a no-op. **Failure mode without it:** filter quickly in
`ExpensesPage` and a slow earlier response could overwrite a faster later one (a race).

**Drill:** In `ExpenseDetailPage`, why is `load` wrapped in `useCallback` with `[id]`, and
what bug appears if you remove the wrapper but keep `load` in the effect's deps array?
(Answer: a new `load` identity each render → effect re-runs every render → infinite fetch
loop.) **Self-grade:** Strong = you can explain the dependency-array → identity link.
