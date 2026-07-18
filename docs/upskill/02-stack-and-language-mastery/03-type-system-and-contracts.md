# Type System & Contracts

C# features this repo leans on, why they're there, and what they prevent. (No TypeScript —
the frontend is plain JSX — so this page is C#-focused.)

## Records (immutable contracts)

**Concept:** `record` types are concise, value-equality, often-immutable data carriers.

**Real code:** every DTO is a record ([`Dtos.cs`](../../../backend/Dtos/Dtos.cs)). Two styles
appear:
- **Positional:** `record LoginRequest([property: Required, EmailAddress] string Email, …)`
  — note `[property: …]` is required to put the attribute on the generated property.
- **Init-property:** `CreateExpenseRequest` with `{ get; init; }` + attributes directly.

Domain value-objects are records too: `ExpenseEvent`, `PolicyFlag`
([`Expense.cs` L4-14](../../../backend/Domain/Expense.cs#L4-L14)), and `Session`
([`DataStore.cs`](../../../backend/Storage/DataStore.cs)).

**Why it matters:** a DTO is a *contract*. Making it a record signals "this is data, compare
by value, don't mutate it." **Failure mode:** if DTOs were the entities themselves, every
API response would risk leaking internal fields (password hash, file path). The records are
the firewall — see [`Mappings.cs`](../../../backend/Services/Mappings.cs).

## Nullable reference types

**Concept:** with `<Nullable>enable</Nullable>`
([csproj](../../../backend/ExpenseManagement.Api.csproj)), `string` is non-null by default
and `string?` is explicitly nullable; the compiler warns on unsafe dereferences.

**Real code:**
- `RejectionReason`, `PaymentReference`, `ReceiptId` are `?` because they're genuinely
  optional ([`Expense.cs` L33-39](../../../backend/Domain/Expense.cs#L33-L39)).
- `required string Email` ([`User.cs`](../../../backend/Domain/User.cs)) forces callers to
  set it — you can't construct a `User` without an email.
- Null-coalescing-throw expresses "must exist":
  `?? throw new NotFoundException(...)` at
  [`ExpenseService` L56-57](../../../backend/Services/ExpenseService.cs#L56-L57).

**Why it matters:** the type system encodes which fields can be absent, so a whole class of
`NullReferenceException` becomes a compile-time warning instead of a 2 a.m. page.

## Pattern matching

**Concept:** `switch` expressions and type patterns make branching on shape concise and
exhaustive-ish.

**Real code:**
- Role-based visibility as a `switch` expression:
  [`ExpenseService.CanView` L190-196](../../../backend/Services/ExpenseService.cs#L190-L196).
- Exception-to-status mapping by type pattern:
  [`ExceptionHandlingMiddleware` L37-43](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L37-L43).
- Property pattern `is { } x` (not-null + capture):
  `if (filter.Status is { } s)` at
  [`ExpenseService` L46-49](../../../backend/Services/ExpenseService.cs#L46-L49) and
  `expense.ReceiptId is { } rid` at
  [L110](../../../backend/Services/ExpenseService.cs#L110).

**Failure mode:** the `_ => …` default arms (e.g. [`CanView` L195](../../../backend/Services/ExpenseService.cs#L195))
default to *deny*. That's the right default for authorization — a new role added to the enum
without a switch arm fails closed (no access), not open. **Senior lesson:** make the safe
case the default.

## Generics & the DI container

**Concept:** generic methods/types parameterized by type.

**Real code:** `AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>`
([`Program.cs` L31](../../../backend/Program.cs#L31)) and `GetRequiredService<DataStore>()`
([`BackgroundJobs` L19](../../../backend/Background/BackgroundJobs.cs#L19)). The queue itself
is built on the generic `Channel<T>`
([`BackgroundTaskQueue` L17](../../../backend/Background/BackgroundTaskQueue.cs#L17)) where
`T` is a delegate type.

## LINQ & deferred execution

**Concept:** LINQ query operators are lazy — nothing runs until you enumerate (`ToList`,
`foreach`, `Count`, etc.).

**Real code:** [`ExpenseService.List` L32-51](../../../backend/Services/ExpenseService.cs#L32-L51)
builds `query` through several `.Where(...)` calls, then materializes once with
`.OrderByDescending(...).ToList()` at L51. The filters are composed, not executed, until the
final `ToList`.

**Why it matters here:** because the source is an in-memory `ConcurrentDictionary.Values`,
deferral is cheap. **The transferable trap:** if this were EF Core `IQueryable`, the same
code would translate to SQL — and the *line where you call `ToList`* decides whether
filtering happens in the database or in memory. Calling `ToList` too early pulls the whole
table into the app, then filters in C# (slow, memory-heavy). This is the #1 EF Core
performance mistake; this repo doesn't use EF Core, but the habit transfers. See
[05/04 performance](../05-quality-engineering/04-performance-thinking.md).

**Drill:** In `List`, what would change if you moved `.ToList()` to right after
`_store.Expenses.Values`? (Here: a wasted copy. In EF Core: a full-table scan.)
**Self-grade:** Strong = you can explain why the answer differs between in-memory and `IQueryable`.
