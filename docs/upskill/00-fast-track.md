# 00 — Fast Track (a weekend win)

Goal: get the app running, trace one feature end to end, make one safe change, and explain
it to someone. ~3–5 hours.

## 0. Prerequisites

| Tool | Needed for | Status in this environment |
| --- | --- | --- |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | backend | **Not installed** (verify with `dotnet --version`) |
| [Node.js 18+](https://nodejs.org) | frontend | Present (`node v22.16.0`, verified) |

> The commands below are **inferred** from the project files, not yet verified by running
> them here. Mark each ✅ once you've run it yourself.

## 1. Run it

Backend (from `backend/`):

```bash
dotnet run        # serves http://localhost:5085  (see Properties/launchSettings.json)
```

Frontend (from `frontend/`, separate terminal):

```bash
npm install
npm run dev       # serves http://localhost:5173, proxies /api -> :5085 (vite.config.js)
```

Open http://localhost:5173. Log in as `employee@demo.com` / `Password123!` (seeded in
[`DataSeeder.cs`](../../backend/Storage/DataSeeder.cs)).

There are no automated tests in the repo yet — that's deliberate; building them is a core
exercise (see [05-quality-engineering](05-quality-engineering/)).

## 2. The 10 files to read first, in order

| # | File | Why |
| --- | --- | --- |
| 1 | [`README.md`](../../README.md) | Product + architecture overview |
| 2 | [`backend/Program.cs`](../../backend/Program.cs) | Composition root — every dependency is wired here |
| 3 | [`Domain/Enums.cs`](../../backend/Domain/Enums.cs) | The whole vocabulary: roles, statuses, categories |
| 4 | [`Domain/Expense.cs`](../../backend/Domain/Expense.cs) | The central entity |
| 5 | [`Domain/ExpenseStateMachine.cs`](../../backend/Domain/ExpenseStateMachine.cs) | The lifecycle rules in one place |
| 6 | [`Services/ExpenseService.cs`](../../backend/Services/ExpenseService.cs) | Core workflow + authorization |
| 7 | [`Services/PolicyEngine.cs`](../../backend/Services/PolicyEngine.cs) | Spend-policy rules |
| 8 | [`Controllers/ExpensesController.cs`](../../backend/Controllers/ExpensesController.cs) | HTTP surface |
| 9 | [`frontend/src/api/client.js`](../../frontend/src/api/client.js) | How the SPA calls the API |
| 10 | [`frontend/src/pages/ExpenseDetailPage.jsx`](../../frontend/src/pages/ExpenseDetailPage.jsx) | Where most user actions originate |

## 3. Trace one flow: "submit an expense"

Follow the call chain (full version in [01/05-key-flows](01-codebase-cartography/05-key-flows.md)):

1. Button in [`ExpenseDetailPage.jsx` L144](../../frontend/src/pages/ExpenseDetailPage.jsx#L144)
   → `api.post('/api/expenses/{id}/submit')`.
2. [`ExpensesController.Submit` L71-77](../../backend/Controllers/ExpensesController.cs#L71-L77)
   resolves the actor, calls the service.
3. [`ExpenseService.Submit` L114-130](../../backend/Services/ExpenseService.cs#L114-L130)
   re-runs policy, blocks if any flag is `Blocking`, else transitions.
4. [`ExpenseService.Transition` L169-179](../../backend/Services/ExpenseService.cs#L169-L179)
   asks [`ExpenseStateMachine.CanTransition` L51-54](../../backend/Domain/ExpenseStateMachine.cs#L51-L54)
   whether `Draft → Submitted` is legal for an `Employee`, then records history.

Notice: the controller does almost nothing; the *rules* live in the service and state
machine. That separation is the single most important idea in this codebase.

## 4. One small safe change

Add a new spend category, `Training`, end to end:

- Add `Training` to [`ExpenseCategory` in Enums.cs](../../backend/Domain/Enums.cs).
- Add a limit for it in [`PolicyEngine.CategoryLimits` L18-29](../../backend/Services/PolicyEngine.cs#L18-L29).
- Add `'Training'` to [`CATEGORIES` in format.js](../../frontend/src/util/format.js).

That's it — the category flows through DTOs and UI as a string. Blast radius is tiny; it
follows an existing pattern. (Full ticket: [06/01 Ticket 1](06-contribution-practice/01-good-first-tickets.md).)

## 5. One check to run

After your change, restart the backend and frontend, create an expense in the new
category, and confirm it appears in the dropdown and on the detail page. (No test suite
exists yet to run — see the caveat in step 1.)

## 6. Teach it back

In 5 sentences, explain to a peer: *Why does adding a category touch three files and not
ten?* (Hint: enums + string serialization + a single category-limit table. The answer is
in how DTOs map enums to strings in [`Mappings.cs` L30](../../backend/Services/Mappings.cs#L30).)

## What this fast track skips

- Authentication internals (PBKDF2, the custom auth handler) — [02/02](02-stack-and-language-mastery/02-framework-mental-models.md) and [03/03](03-architecture-and-patterns/03-validation-auth-and-permissions.md).
- The background queue and async reliability — [03/04](03-architecture-and-patterns/04-side-effects-async-and-reliability.md).
- Testing, debugging, performance, security depth — module [05](05-quality-engineering/).
- The known risks (e.g. concurrency on shared mutable entities) — [08/risk-register](08-reference/risk-register.md).
