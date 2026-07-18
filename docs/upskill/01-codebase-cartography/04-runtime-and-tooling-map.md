# Runtime & Tooling Map

## Where code actually runs

| Tier | Runtime | Entry point | Notes |
| --- | --- | --- | --- |
| Browser (client) | V8 / the user's browser | [`main.jsx`](../../../frontend/src/main.jsx) | React SPA; holds the bearer token in `localStorage` |
| Dev server | Node + Vite | `npm run dev` ([vite.config.js](../../../frontend/vite.config.js)) | Bundles JS, proxies `/api` → `:5085` |
| API (server) | .NET 8 CLR | [`Program.cs`](../../../backend/Program.cs) | Kestrel web server on `:5085` |
| Background worker | .NET 8 CLR, same process | [`QueuedHostedService`](../../../backend/Background/QueuedHostedService.cs) | A `BackgroundService` thread draining the queue |

The worker is **in the same process** as the API — not a separate service. That's a
deliberate simplification with real consequences (see the reliability discussion in
[03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md)).

## Build & tooling

| Concern | Backend | Frontend |
| --- | --- | --- |
| Build tool | MSBuild via `dotnet` SDK | Vite (Rollup under the hood) |
| Project file | [`ExpenseManagement.Api.csproj`](../../../backend/ExpenseManagement.Api.csproj) (`net8.0`, nullable on, implicit usings on) | [`package.json`](../../../frontend/package.json) |
| Dependencies | **None** (only the bundled ASP.NET Core shared framework) | react, react-dom, react-router-dom; dev: vite, @vitejs/plugin-react |
| Run | `dotnet run` | `npm run dev` |
| Output | DLL + Kestrel host | static bundle (`vite build`) |

The zero-dependency backend is the standout fact: there is no `Directory.Packages.props`,
no `global.json` pinning, no `nuget.config`. Everything you'll learn here is plain
framework, which makes it more transferable, not less.

## Configuration & secrets

| Setting | Where | Default |
| --- | --- | --- |
| Receipt upload dir | `Storage:ReceiptUploadPath` | `uploads/` (relative to content root) — [appsettings.json](../../../backend/appsettings.json) |
| Max receipt size | `Storage:MaxReceiptSizeBytes` | 5 MB |
| Session lifetime | `Auth:SessionLifetimeHours` | 8 — read at [`AuthService` L20](../../../backend/Security/AuthService.cs#L20) |
| Allowed CORS origins | `Cors:AllowedOrigins` | `http://localhost:5173` — used at [`Program.cs` L37-41](../../../backend/Program.cs#L37-L41) |
| API URL (dev) | `applicationUrl` | `http://localhost:5085` — [launchSettings.json](../../../backend/Properties/launchSettings.json) |

**There are no real secrets in this project** — passwords are hashed demo values, tokens
are random in memory, there's no DB connection string or API key. In a production version,
this is exactly where a connection string, a JWT signing key, and a payment-provider key
would live, and you'd move them out of `appsettings.json` into user-secrets / environment
variables / a vault. Worth noting now because it's the #1 thing juniors get wrong when they
"make it real."

## Config binding patterns to learn from

- Typed defaults: `config.GetValue("Auth:SessionLifetimeHours", 8)` — [`AuthService` L20](../../../backend/Security/AuthService.cs#L20).
- Section → array: `GetSection("Cors:AllowedOrigins").Get<string[]>()` — [`Program.cs` L37](../../../backend/Program.cs#L37).
- Fallback when missing: the `?? new[] { ... }` on the same line. Note this means a typo in
  config silently falls back to localhost rather than failing loudly — *investigate* whether
  that's the behavior you'd want in production.
