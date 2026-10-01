# Verification Log

What was actually inspected vs. assumed while writing this suite. Distrust anything not
listed as verified.

**Date:** 2026-06-09 · **Author:** documentation pass (no product code changed).

## Environment checks (commands run)
| Command | Result |
| --- | --- |
| `dotnet --version` | **not found** — .NET SDK is not installed in this environment |
| dotnet probe of common install paths | none found (confirmed absent) |
| `node --version` | `v22.16.0` ✅ |
| `npm` location | `C:\Program Files\nodejs\npm.ps1` ✅ |

## What this means
- **The application was never compiled or run** during documentation. "It builds / it runs"
  is **unverified**. The run commands in [00-fast-track](../00-fast-track.md) and the
  [cheatsheet](command-cheatsheet.md) are inferred from project files.
- All **code anchors (file + line numbers) were verified by reading the files** during this
  pass — the heavily-cited backend files were opened and line numbers confirmed against the
  cited ranges.

## Files read and line-anchored (verified)
- `Domain/ExpenseStateMachine.cs`, `Domain/Expense.cs` (structure), `Domain/Enums.cs` (structure)
- `Services/ExpenseService.cs`, `Services/PolicyEngine.cs`, `Services/Mappings.cs`,
  `Services/ReceiptService.cs`, `Services/ReceiptStorage.cs`
- `Security/AuthService.cs`, `Security/TokenAuthenticationHandler.cs`, `Security/UserContext.cs`
- `Background/BackgroundJobs.cs`, `Background/BackgroundTaskQueue.cs`, `Background/QueuedHostedService.cs`
- `Middleware/ExceptionHandlingMiddleware.cs`, `Program.cs`
- `Controllers/ExpensesController.cs`, `Controllers/ReceiptsController.cs`
- `frontend/src/api/client.js`, `frontend/src/pages/ExpenseDetailPage.jsx`

## Files referenced from authored content (line numbers approximate — re-verify before relying)
- `Dtos/Dtos.cs`, `Storage/DataStore.cs`, `Storage/DataSeeder.cs`, `Domain/User.cs`,
  `Domain/Receipt.cs`, `Domain/DomainExceptions.cs`, `Security/PasswordHasher.cs`,
  `Controllers/AuthController.cs`, `Controllers/ReportsController.cs`,
  `Services/ReportingService.cs`, and the remaining `frontend/src` files. These are cited at
  file or block level; if a line number is off by a little, the surrounding code is correct.

## Known uncertainties / "investigate" items flagged in the docs
- Whether the app compiles cleanly on first `dotnet run` (unverified — no SDK).
- Severity of the concurrency/data-race risk (R3) — needs load testing to quantify.
- The partial timing-equalization in `AuthService.Login` (returns early on unknown user).
- Whether `NextStates` is truly unused (claimed in a kata; confirm with a search before deleting).
- Exact line numbers in the "approximate" file group above.

## Gaps (not covered by this suite)
- No runtime screenshots or recorded traces (couldn't run the app).
- No test results (no tests exist).
- Frontend pages other than `ExpenseDetailPage`/`client.js` are described from authored
  content, not re-read line-by-line this pass.

## Recommended first verification by the learner
1. Install .NET 8 SDK, `dotnet run`, `npm install && npm run dev`.
2. Walk the demo in the [root README](../../../README.md).
3. Confirm 3–4 anchors in [key flows](../01-codebase-cartography/05-key-flows.md) point where
   the docs say. If they do, trust the rest; if a line drifted, it's a doc bug, not a code bug.
