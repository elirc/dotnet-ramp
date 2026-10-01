# System Map

## Two deployables, one repo

```
┌──────────────────────────┐         HTTP /api (bearer token)      ┌───────────────────────────┐
│  frontend/ (React + Vite)│ ───────────────────────────────────▶ │  backend/ (ASP.NET Core 8)│
│  SPA, runs in the browser│ ◀───────────────────────────────────  │  Web API, runs on server  │
│  dev: localhost:5173     │   JSON / ProblemDetails / file blobs   │  dev: localhost:5085      │
└──────────────────────────┘                                        └───────────────────────────┘
        │ vite proxy /api -> :5085 (frontend/vite.config.js)                 │
                                                                    in-memory DataStore (singleton)
                                                                    + local disk for receipts
```

There is no database and no message broker. "Persistence" is a process-lifetime singleton;
"the queue" is an in-process `Channel`. Restarting the backend wipes all data.

## Backend project layout

Single project, `ExpenseManagement.Api` ([csproj](../../../backend/ExpenseManagement.Api.csproj)),
organized by responsibility (a "layered folders in one assembly" structure, not separate
class libraries):

| Folder | Owns | Must NOT own |
| --- | --- | --- |
| `Domain/` | Entities, enums, the **state machine**, domain exceptions. Pure business vocabulary. | HTTP, DI, storage details |
| `Storage/` | `DataStore` (the in-memory DB) + `DataSeeder` | Business rules |
| `Security/` | Password hashing, sessions, the auth handler, `IUserContext` | Expense logic |
| `Services/` | The use cases: `ExpenseService`, `ReceiptService`, `ReportingService`, `PolicyEngine`, `ReceiptStorage`, DTO `Mappings` | HTTP status codes, routing |
| `Background/` | The task queue, hosted worker, job definitions | Request-scoped state |
| `Middleware/` | Exception → `ProblemDetails` translation | Business decisions |
| `Controllers/` | HTTP routing, model binding, calling services | Business rules, authorization logic (delegated to services) |
| `Dtos/` | Request/response contracts | Entities |
| `Program.cs` | Composition root — wires everything | Logic of any kind |

**The dependency direction that matters:** Controllers → Services → Domain + Storage.
Controllers depend on services; services depend on the domain and the store; the domain
depends on nothing. If you ever see `Domain/` importing `Microsoft.AspNetCore.*`, a
boundary has leaked.

## Ownership: who decides what

| Concern | Single source of truth | Anchor |
| --- | --- | --- |
| What status transitions are legal | `ExpenseStateMachine` | [L19-58](../../../backend/Domain/ExpenseStateMachine.cs#L19-L58) |
| Who can see / act on an expense | `ExpenseService` (CanView / GetOwned / EnsureManagesOwner) | [L181-205](../../../backend/Services/ExpenseService.cs#L181-L205) |
| What violates spend policy | `PolicyEngine.Evaluate` | [L34-66](../../../backend/Services/PolicyEngine.cs#L34-L66) |
| How errors become HTTP responses | `ExceptionHandlingMiddleware` | [L35-63](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L35-L63) |
| What the client may do (UI affordances) | `Mappings.AvailableActions` | [L45-72](../../../backend/Services/Mappings.cs#L45-L72) |

Note the last two rows: authorization is enforced **server-side** in the service, and
*also* surfaced to the client as `availableActions` so the UI can show/hide buttons. The
UI list is a convenience, not a security control — the service is the gate.

## Frontend layout

| Path | Role |
| --- | --- |
| [`src/main.jsx`](../../../frontend/src/main.jsx) | Mounts React, wraps app in Router + AuthProvider |
| [`src/App.jsx`](../../../frontend/src/App.jsx) | Routes + `RequireAuth` gate |
| [`src/auth/AuthContext.jsx`](../../../frontend/src/auth/AuthContext.jsx) | Login/logout/me, holds the user |
| [`src/api/client.js`](../../../frontend/src/api/client.js) | `fetch` wrapper: token, JSON, error shape |
| [`src/pages/`](../../../frontend/src/pages/) | One component per screen |
| [`src/components/`](../../../frontend/src/components/) | `Layout` (nav) + `StatusBadge` |
| [`src/util/format.js`](../../../frontend/src/util/format.js) | Money/date formatting + enum string lists |

## Public vs. internal surface

- **Public (contracts you must not break casually):** the HTTP routes in `Controllers/`
  and the JSON shapes in `Dtos/`. The frontend depends on these field names (camelCased by
  ASP.NET's default serializer). Changing `ExpenseResponse` is a breaking change.
- **Internal (free to refactor):** everything in `Services/`, `Storage/`, `Background/`,
  `Domain/`. As long as the DTOs and routes stay stable, the SPA doesn't care.

This public/internal split is the practical meaning of "API contract." See
[03/01-boundaries-and-layers](../03-architecture-and-patterns/01-boundaries-and-layers.md).
