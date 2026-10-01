# Command Cheatsheet

**Legend:** ✅ verified in this environment · ⚙️ inferred from project files (not run here).

## Environment
| Command | Purpose | Status |
| --- | --- | --- |
| `dotnet --version` | Check SDK (currently **not installed**) | ✅ (returns not-found) |
| `node --version` | Check Node (`v22.16.0`) | ✅ |

## Backend (`backend/`)
| Command | Purpose | Status |
| --- | --- | --- |
| `dotnet run` | Build + run API on `http://localhost:5085` | ⚙️ |
| `dotnet build` | Compile only | ⚙️ |
| `dotnet watch run` | Run with hot reload | ⚙️ |
| `dotnet run --launch-profile http` | Use the defined profile | ⚙️ |

## Tests (after scaffolding — no test project exists yet)
| Command | Purpose | Status |
| --- | --- | --- |
| `dotnet new xunit -o backend.Tests` | Create test project | ⚙️ |
| `dotnet add backend.Tests reference backend/ExpenseManagement.Api.csproj` | Link to API | ⚙️ |
| `dotnet test` | Run all tests | ⚙️ |
| `dotnet test --filter "FullyQualifiedName~Transitions"` | Run one test | ⚙️ |

## Frontend (`frontend/`)
| Command | Purpose | Status |
| --- | --- | --- |
| `npm install` | Install deps | ⚙️ |
| `npm run dev` | Vite dev server on `:5173`, proxy `/api`→`:5085` | ⚙️ |
| `npm run build` | Production build | ⚙️ |
| `npm run preview` | Serve the build locally | ⚙️ |

## Git (etiquette)
| Command | Purpose |
| --- | --- |
| `git switch -c feature/<name>` | Branch before changing |
| `git add -p` | Stage hunks deliberately |
| `git status --short` | Quick state check |

## Demo accounts (password `Password123!`)
`employee@demo.com` · `dana@demo.com` · `manager@demo.com` · `finance@demo.com`
(seeded in [`DataSeeder.cs`](../../../backend/Storage/DataSeeder.cs))

## Not present (would be added when hardening)
Docker, CI workflow, `global.json`, migrations — see [tickets](../06-contribution-practice/).
