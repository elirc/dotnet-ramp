# Data Model & Persistence

## The model

```
User ──manages──▶ User            Expense ──has 0..1──▶ Receipt
 (ManagerId?)                       (ReceiptId?)
   │                                  │
   └────── owns (EmployeeId) ─────────┘
                                     Expense ──has many──▶ ExpenseEvent (history, embedded)
                                     Expense ──has many──▶ PolicyFlag   (latest eval, embedded)
```

| Entity | Key | Relationships | File |
| --- | --- | --- | --- |
| `User` | `Id` (Guid) | self-ref `ManagerId?` | [`User.cs`](../../../backend/Domain/User.cs) |
| `Expense` | `Id` | `EmployeeId`, `ReceiptId?` | [`Expense.cs`](../../../backend/Domain/Expense.cs) |
| `Receipt` | `Id` | `ExpenseId` | [`Receipt.cs`](../../../backend/Domain/Receipt.cs) |
| `ExpenseEvent` | — (embedded) | inside `Expense.History` | [`Expense.cs` L4-11](../../../backend/Domain/Expense.cs#L4-L11) |
| `PolicyFlag` | — (embedded) | inside `Expense.PolicyFlags` | [`Expense.cs` L14](../../../backend/Domain/Expense.cs#L14) |

Note the **embedded** collections: `History` and `PolicyFlags` live *inside* the `Expense`
object rather than as separate tables. In the in-memory model that's just a `List`. With a
relational DB you'd decide: child tables (normalized) vs. JSON columns (document-style). The
audit trail (`History`) is a good candidate for an append-only child table.

## The store

[`DataStore.cs`](../../../backend/Storage/DataStore.cs) is four `ConcurrentDictionary`s
(`Users`, `Expenses`, `Receipts`, `Sessions`) plus two query helpers (`FindUserByEmail`,
`DirectReportsOf`). Registered as a **singleton** ([`Program.cs` L16](../../../backend/Program.cs#L16))
so it survives across requests and is shared with the background worker.

**"Transactions" here are just object mutation.** When `Submit` changes status and appends
history ([`ExpenseService` L176-178](../../../backend/Services/ExpenseService.cs#L176-L178)),
there's no commit — the object is already in the dictionary, so the change is "persisted" the
instant it's made. This is convenient and also the source of the concurrency caveats (no
isolation, no atomic multi-entity update).

## What changes when you add a real database (EF Core)

This is the most valuable thought-experiment in the repo. A migration to EF Core would
touch **only the storage layer** if the boundaries hold:

| Concern | Today (in-memory) | With EF Core |
| --- | --- | --- |
| Store | `ConcurrentDictionary` | `DbContext` + `DbSet<T>` (scoped, not singleton!) |
| Read | `_store.Expenses.Values.Where(...)` | `_db.Expenses.Where(...)` translated to SQL |
| Write | mutate object in place | `_db.Add/Update` + `await _db.SaveChangesAsync()` |
| Atomicity | none | a transaction / single `SaveChanges` |
| Lifetime | singleton | **scoped** — and the background worker already creates a scope per job, so it's ready |

**The gotcha you must call out:** `DbContext` is **not thread-safe** and must be **scoped**,
whereas today's `DataStore` is a singleton. Code that captures the store in a long-lived
field (or a singleton service) would break. The good news: the worker already uses
`CreateScope()` ([`QueuedHostedService` L42](../../../backend/Background/QueuedHostedService.cs#L42)),
so it's structurally prepared.

**N+1 preview:** `Mappings.ToResponse` does `store.Users.GetValueOrDefault(e.EmployeeId)`
per expense ([`Mappings` L19](../../../backend/Services/Mappings.cs#L19)). In memory that's a
dictionary hit (free). Against a DB, mapping a list of 100 expenses would fire 100 user
lookups — the classic **N+1**. The fix (a join / `Include` / batch-load) is covered in
[05/04](../05-quality-engineering/04-performance-thinking.md). Recognizing that this *current,
harmless* code becomes a *performance bug* under a different store is exactly the senior
muscle to build.

## Safe schema-change guidance (for the future DB version)

- Add columns nullable or with defaults; backfill; then enforce — never add a non-null
  column with no default to a populated table in one step.
- Expand-then-contract for renames: add new, dual-write, migrate readers, drop old.
- Keep migrations forward-only and reviewed; a rollback plan per migration.

None of this applies yet (no migrations), but it's the checklist you'll need the day this
grows up. Drill: sketch the migration that turns `Expense.History` into an
`ExpenseEvents` table without losing data.
