# Tooling & Build System

## Backend: the .NET SDK + MSBuild

**The project file is the build.** [`ExpenseManagement.Api.csproj`](../../../backend/ExpenseManagement.Api.csproj)
declares:

| Element | Value | Meaning |
| --- | --- | --- |
| `Sdk="Microsoft.NET.Sdk.Web"` | — | Pulls in the ASP.NET Core shared framework + web build targets |
| `<TargetFramework>` | `net8.0` | Compiles against .NET 8 |
| `<Nullable>` | `enable` | Nullable reference types on (see [03](03-type-system-and-contracts.md)) |
| `<ImplicitUsings>` | `enable` | Common namespaces auto-imported |
| **No `<PackageReference>`** | — | Zero NuGet dependencies |

**Implicit usings explain the "missing" usings.** You won't see `using System;` or
`using Microsoft.AspNetCore.Http;` in most files — the Web SDK injects a default set
globally. That's why `IFormFile` works in [`ReceiptStorage.cs`](../../../backend/Services/ReceiptStorage.cs)
with no explicit import, and `ILogger<>`, `IConfiguration`, `GetRequiredService` work
everywhere. Files only add `using` for namespaces *outside* that set —
e.g. `System.Threading.Channels` at
[`BackgroundTaskQueue` L1](../../../backend/Background/BackgroundTaskQueue.cs#L1),
`System.Security.Cryptography` in `PasswordHasher`, `System.Text.Json` in the middleware.

**Drill:** Find one `using` that *is* explicit and explain why it isn't covered by implicit
usings. (Good candidates: the three just listed.)

## Common commands

| Command | Does | Verified? |
| --- | --- | --- |
| `dotnet run` (in `backend/`) | Build + run on `:5085` | inferred |
| `dotnet build` | Compile only | inferred |
| `dotnet watch run` | Hot-reload on file change | inferred |
| `dotnet test` | Run tests — **no test project exists yet** | n/a |

Full list in [08/command-cheatsheet](../08-reference/command-cheatsheet.md).

## Frontend: Vite

[`package.json`](../../../frontend/package.json) scripts: `dev`, `build`, `preview`. Vite
uses esbuild for dev (fast) and Rollup for production builds.

**The dev proxy is the key config:** [`vite.config.js`](../../../frontend/vite.config.js)
forwards `/api/*` to `http://localhost:5085`. This is why the frontend can call `/api/...`
as if same-origin (and why CORS rarely even triggers in dev — the browser sees one origin,
`:5173`). In production you'd serve the built SPA behind the same host as the API, or
configure CORS for the real origin.

**Failure mode:** if the backend isn't running on `:5085`, every `fetch` returns a proxy
error, not a clean 401/404 — a common "why is login broken" red herring. Check the backend
terminal first.

## What's deliberately absent

No Docker, no CI workflow, no `global.json`, no lockfile committed for the backend (there
are no packages to lock), no central package management. For a learning repo this keeps the
surface minimal. For a *real* project, adding `global.json` (pin the SDK), a CI workflow
(build + test on PR), and a `Dockerfile` are natural early hardening steps — three of them
appear as tickets in [06](../06-contribution-practice/).
