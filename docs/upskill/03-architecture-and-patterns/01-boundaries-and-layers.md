# Boundaries & Layers

## The layering

```
HTTP ──▶ Controllers ──▶ Services ──▶ Domain (rules) + Storage (data)
            (thin)        (use cases)      (no dependencies)
              ▲               │
           DTOs  ◀── Mappings ┘
```

| Layer | Owns | Must NOT own | Evidence it holds |
| --- | --- | --- | --- |
| Controllers | Routing, model binding, HTTP status selection | Business rules, authorization logic | [`ExpensesController`](../../../backend/Controllers/ExpensesController.cs) actions are 3-4 lines each |
| Services | Use cases, authorization, orchestration | HTTP types, routing | `ExpenseService` has no `Microsoft.AspNetCore.Mvc` import |
| Domain | Entities, the state machine, invariants | HTTP, DI, storage | `Domain/` imports nothing framework-specific |
| Storage | Holding data | Business decisions | `DataStore` is dictionaries + 2 helpers |
| DTOs | The wire contract | Behavior | `Dtos.cs` is pure data records |

## Why controllers are thin

Compare the controller and the service for "approve":

- Controller [`Approve` L79-86](../../../backend/Controllers/ExpensesController.cs#L79-L86):
  attribute gate, resolve actor, call service, map result.
- Service [`Approve` L132-137](../../../backend/Services/ExpenseService.cs#L132-L137):
  the actual rules (can-view, manages-owner, legal transition).

**Transferable principle:** controllers are an *adapter* from HTTP to use cases. Keeping
them thin means the rules are testable without spinning up the web stack, and a second entry
point (a CLI, a gRPC service, a scheduled job) could reuse the same service unchanged.

## Boundary leaks to watch for (review red flags)

| Leak | What it looks like here | Why it's bad |
| --- | --- | --- |
| Entity returned from a controller | Returning `Expense` instead of `ExpenseResponse` | Leaks internal fields; couples the wire to the DB shape |
| Authorization in the controller | Doing the `ManagesOwner` check in the action | Splits the rule across layers; the next caller forgets it |
| HTTP types in a service | A service returning `IActionResult` | Service can't be reused off the web thread |
| Domain importing framework | `Domain/` using `Microsoft.AspNetCore.*` | Domain becomes un-unit-testable in isolation |
| Storage making decisions | `DataStore` filtering by role | Two sources of truth for visibility |

The single most important boundary in this codebase: **authorization lives in the service,
not the controller.** The `[Authorize(Roles=…)]` attribute is a coarse pre-filter; the real
resource check (`EnsureManagesOwner`, `CanView`, `GetOwned`) is in `ExpenseService`. See
[03/03](03-validation-auth-and-permissions.md).

## The DTO ↔ entity boundary

`Mappings` ([`Mappings.cs`](../../../backend/Services/Mappings.cs)) is the only place
entities become DTOs. This is what lets the `User` entity hold `PasswordHash`/`PasswordSalt`
([`User.cs`](../../../backend/Domain/User.cs)) while `UserResponse`
([`Dtos.cs`](../../../backend/Dtos/Dtos.cs)) never includes them. **Drill:** add a field to
`Expense` and confirm it does *not* appear in any API response until you also add it to a DTO
and the mapping — that gap is the boundary doing its job.
