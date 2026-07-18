# Testing Strategy

## The test pyramid for this codebase

| Layer | What to test | Cost | Examples here |
| --- | --- | --- | --- |
| **Unit** (most) | Pure logic, no I/O | cheap | `ExpenseStateMachine`, `PolicyEngine`, `PasswordHasher` |
| **Service** | Use cases against the in-memory store | cheap (no DB!) | `ExpenseService` authorization, submit gating |
| **API/integration** | HTTP in, JSON out, via `WebApplicationFactory` | medium | login → submit → approve happy path; 403 on IDOR |
| **UI/E2E** (fewest) | Browser flows | expensive/flaky | login + create + submit in the SPA |

**This repo's gift:** because the "database" is in-memory and deterministic, *service-level*
tests are as cheap as unit tests and far more valuable than mocks. You can construct a
`DataStore`, seed two users, and exercise real authorization with zero infrastructure.

## What to test first (highest value)

1. **The state machine** — every legal edge + a sample of illegal ones, per role.
   ([`ExpenseStateMachine`](../../../backend/Domain/ExpenseStateMachine.cs))
2. **Authorization / IDOR** — a manager cannot approve another team's expense.
   ([`EnsureManagesOwner`](../../../backend/Services/ExpenseService.cs#L198-L202))
3. **Policy gating** — submit is blocked iff a Blocking flag exists.
   ([`Submit`](../../../backend/Services/ExpenseService.cs#L114-L130))
4. **Password hashing** — verify round-trips; wrong password fails.

These four cover the riskiest logic and are pure or near-pure — ideal first tests.

## What NOT to test

- The framework (model binding, DI, JSON serialization) — trust it.
- Trivial mappings (`Mappings.ToResponse`) beyond one "doesn't leak secrets" assertion.
- The simulated OCR/payment *timings* (`Task.Delay`) — test the *state outcome*, not the delay.
- Getters/`IsEditable`-style one-liners individually — they're covered transitively.

## Test design principles to practice here

- **Isolation:** each test builds its own `DataStore`; no shared static state.
- **Determinism:** `PolicyEngine` uses `DateTime.UtcNow`
  ([L37](../../../backend/Services/PolicyEngine.cs#L37)) — for date-rule tests, this is a
  *seam to fix*: inject a clock (`Func<DateTime>` or `TimeProvider`) so "future date" tests
  don't depend on the wall clock. Good first refactor-for-testability.
- **The async seam:** background jobs take an `IServiceProvider`; in tests you can call the
  job delegate directly with a test provider and `await` it — no waiting on the real worker.
- **Builders:** a small `ExpenseBuilder` keeps tests readable (see [02](02-writing-tests-here.md)).

## Setup note (honest)

There is no test project yet. Adding one (`dotnet new xunit`) **does** pull xUnit NuGet
packages — which is fine and normal, but worth flagging given the backend's deliberate
zero-dependency stance for *production* code. Test dependencies are separate from runtime
dependencies; adding them doesn't compromise the zero-dep API.
