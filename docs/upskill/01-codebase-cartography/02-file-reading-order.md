# File Reading Order

Read in this order; it builds from vocabulary → rules → wiring → edges → UI. Each row says
what to look for. Split by stage so you can stop where it stops being useful.

## Stage 1 — Orientation (vocabulary & shape)

| # | File | Look for |
| --- | --- | --- |
| 1 | [`README.md`](../../../README.md) | The product and the run commands |
| 2 | [`Domain/Enums.cs`](../../../backend/Domain/Enums.cs) | The 5 enums = the whole vocabulary. Note `PolicySeverity` (Warning vs Blocking). |
| 3 | [`Domain/User.cs`](../../../backend/Domain/User.cs) | `ManagerId` — the org hierarchy is one nullable field |
| 4 | [`Domain/Expense.cs`](../../../backend/Domain/Expense.cs) | Entity shape; `History` (audit trail) and `IsEditable` (computed) |
| 5 | [`Domain/Receipt.cs`](../../../backend/Domain/Receipt.cs) | Why `StoredFileName` ≠ `OriginalFileName` |
| 6 | [`Domain/ExpenseStateMachine.cs`](../../../backend/Domain/ExpenseStateMachine.cs) | The transition table. This is the heart of the domain. |

## Stage 2 — Junior (rules & flow)

| # | File | Look for |
| --- | --- | --- |
| 7 | [`Storage/DataStore.cs`](../../../backend/Storage/DataStore.cs) | "The database" is 4 dictionaries + helpers |
| 8 | [`Storage/DataSeeder.cs`](../../../backend/Storage/DataSeeder.cs) | The demo users and the manager→employee links |
| 9 | [`Services/PolicyEngine.cs`](../../../backend/Services/PolicyEngine.cs) | Each rule; which are Blocking vs Warning |
| 10 | [`Services/ExpenseService.cs`](../../../backend/Services/ExpenseService.cs) | The use cases. Read `Transition` (L169) last — it ties to the state machine. |
| 11 | [`Controllers/ExpensesController.cs`](../../../backend/Controllers/ExpensesController.cs) | How thin a controller can be; where `[Authorize(Roles=…)]` sits |
| 12 | [`Dtos/Dtos.cs`](../../../backend/Dtos/Dtos.cs) | The contract; DataAnnotations validation |
| 13 | [`Services/Mappings.cs`](../../../backend/Services/Mappings.cs) | Entity→DTO; `AvailableActions` computing UI affordances |

## Stage 3 — Mid-level (wiring, security, async)

| # | File | Look for |
| --- | --- | --- |
| 14 | [`Program.cs`](../../../backend/Program.cs) | DI lifetimes (singleton vs scoped), pipeline order |
| 15 | [`Security/PasswordHasher.cs`](../../../backend/Security/PasswordHasher.cs) | PBKDF2 params; constant-time compare |
| 16 | [`Security/AuthService.cs`](../../../backend/Security/AuthService.cs) | Token issuance + session validation |
| 17 | [`Security/TokenAuthenticationHandler.cs`](../../../backend/Security/TokenAuthenticationHandler.cs) | How a token becomes a `ClaimsPrincipal` |
| 18 | [`Security/UserContext.cs`](../../../backend/Security/UserContext.cs) | Bridging claims → the `User` entity |
| 19 | [`Middleware/ExceptionHandlingMiddleware.cs`](../../../backend/Middleware/ExceptionHandlingMiddleware.cs) | Exceptions → `ProblemDetails` |
| 20 | [`Background/BackgroundTaskQueue.cs`](../../../backend/Background/BackgroundTaskQueue.cs) | A `Channel` as a queue; bounded backpressure |
| 21 | [`Background/QueuedHostedService.cs`](../../../backend/Background/QueuedHostedService.cs) | The worker loop; per-job scope; error isolation |
| 22 | [`Background/BackgroundJobs.cs`](../../../backend/Background/BackgroundJobs.cs) | The two jobs; the system transition |
| 23 | [`Services/ReceiptService.cs`](../../../backend/Services/ReceiptService.cs) | Upload → store → enqueue orchestration |
| 24 | [`Services/ReceiptStorage.cs`](../../../backend/Services/ReceiptStorage.cs) | Upload validation; path-traversal defense |

## Stage 4 — Frontend & the seams

| # | File | Look for |
| --- | --- | --- |
| 25 | [`frontend/src/api/client.js`](../../../frontend/src/api/client.js) | How errors are surfaced; the 204 special-case |
| 26 | [`frontend/src/auth/AuthContext.jsx`](../../../frontend/src/auth/AuthContext.jsx) | Token bootstrap; the `me` round-trip |
| 27 | [`frontend/src/pages/ExpenseDetailPage.jsx`](../../../frontend/src/pages/ExpenseDetailPage.jsx) | `availableActions` driving the UI; polling background work |

By file 27 you can trace any feature both directions. Senior move: after reading, close the
files and try to redraw the system map from memory — the gaps are your next study targets.
