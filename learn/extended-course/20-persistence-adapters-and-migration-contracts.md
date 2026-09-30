# 20. Persistence adapters and migration contracts

Read [DataStore](../../backend/Storage/DataStore.cs), [ExpenseService](../../backend/Services/ExpenseService.cs), [ReceiptService](../../backend/Services/ReceiptService.cs), and [ReportingService](../../backend/Services/ReportingService.cs). This is a proposed migration workshop. It examines what would actually change when replacing shared in-memory objects with durable storage, rather than accepting the store comment's suggestion that persistence is a trivial swap.

## Inventory direct storage assumptions

The services read and write the dictionaries directly. They retrieve mutable entity references, change properties, append history, and expect those changes to be visible through the stored object. A database adapter returning detached values would not automatically persist those mutations.

Therefore, replacing only DataStore's concrete collection type is not enough to preserve behavior. The design needs explicit save boundaries, transaction semantics, query interfaces, and concurrency handling. This observation does not make the current teaching store bad; it identifies the assumptions a migration must address.

Create an inventory by operation. Create inserts a new expense. Update mutates an existing owned editable expense and refreshes policy. Submit changes flags and possibly lifecycle. Reject can mutate reason before failure. Receipt upload coordinates disk and metadata. Reporting enumerates visible expenses and aggregates them. Each operation has a different persistence shape.

## Choose the aggregate boundary

One proposed expense aggregate contains core expense fields and transition history. Receipt metadata may be part of the aggregate or a related entity, while file bytes remain an external resource. Policy flags may be stored derived state or computed projections. These choices affect transaction size and read cost.

If history is stored separately, accepted transition and event append should share a coherent commit boundary. If policy flags are computed on read, report and detail performance change. If they are stored, receipt processing must keep them current or expose eventual consistency. There is no universal answer; the workshop requires you to justify one model against the application's actual workflows.

A relational schema proposal should include owner identity, manager relationships, receipt references, status, version, timestamps, and event ordering. Do not omit relationships because the current dictionaries make them easy to look up. Durable constraints can prevent some invalid states that in-memory code currently assumes away.

## Preserve authorization inside queries

ExpenseService.List currently filters visibility before optional status, category, and date filters. A database query should preserve that scope in the query itself where practical, rather than loading every expense and filtering sensitive data afterward. The exact expression depends on the chosen data access layer, which this course does not install or prescribe.

A manager query needs own expenses plus direct reports. A Finance query has the selected broad scope. An Employee query needs owner equality. Test the generated or executed query with synthetic users and overlapping dates so filtering mistakes cannot hide behind an empty dataset.

Do not treat a repository method named ListVisible as proof that its implementation enforces visibility. Write adapter contract tests with the same authority matrix used at the service layer. The method name is a promise; the test establishes whether the adapter keeps it.

## Version checks change update semantics

A proposed version column lets an update require the version observed by the command. The storage operation should atomically affect a row only when identity and expected version match, then advance the version. A zero-row result can mean missing record or conflict, depending on the query and contract.

Decide whether the API distinguishes those outcomes. Returning a conflict with current version can support recovery, while exposing existence across authorization boundaries may be inappropriate. The service should resolve authority before revealing details according to the chosen policy.

The accepted transition and history append must not separate into a successful status update followed by a failed event insert without a recovery rule. A transaction or another explicit atomic protocol should preserve their relationship. The in-memory list append is not evidence that a future adapter naturally has that property.

## Uniqueness and foreign keys need meaning

Expense IDs are unique entity identities. Receipt IDs identify metadata, while an expense has one current ReceiptId. A schema can enforce some references, but replacement and cleanup still require application logic. A foreign key does not delete an unreferenced disk file.

A unique work identity can support durable handoff, but its scope must match the command contract. A unique constraint on expense ID alone might prevent legitimate later operations if the workflow expands. A compound key including operation type and command identity can be more precise, but should be justified by actual retry semantics.

Avoid adding constraints solely because they are easy to express. First write the invalid state they prevent and the user-visible outcome when a command conflicts. Then design adapter error translation so a raw database exception does not become an unexplained server error for an expected business conflict.

## Migration must account for existing values

Current direct service calls can store currency with spaces after uppercasing, and domain properties can be assigned values outside ordinary HTTP validation. A migration that introduces stricter constraints may encounter such values in fixtures or persisted exports. Even in a teaching project, the plan should distinguish cleaning data from changing acceptance rules.

Prepare a synthetic migration dataset with valid rows, dangling receipt references, unknown owner references, inconsistent history, and mixed currency text. State which rows migrate, which are normalized with recorded justification, and which require review. Do not silently discard records to make a migration script succeed.

A rollback plan should preserve the ability to recover original data and explain which transformations are reversible. For a course exercise, use a disposable dataset and never operate on the user's actual application data. The deliverable can be a schema and migration plan without executing any database change.

## Query behavior can change under translation

The current code uses LINQ over in-memory objects. A database provider may translate only certain expressions, handle collation differently, and order null or equal values differently. A migration test should verify the selected query semantics rather than assume every in-memory comparison has an identical database meaning.

DateOnly range inclusivity, case-insensitive email lookup, enum storage, and timestamp precision deserve explicit fixtures. Monetary decimal precision and scale should be chosen to preserve the accepted amount range and required fractional precision. The course does not claim a particular provider configuration because none is introduced here.

Reports should be tested with grouped totals and visibility together. An optimization that moves aggregation into the database must not broaden scope or change which statuses contribute to TotalApproved. Preserve the worked six-status oracle before adding large-scale performance tests.

## Adapter tests should target contracts

Use the same repository contract suite against an in-memory adapter and a disposable database adapter when one is implemented. Include create/read, authorized filtering, stale version rejection, transition-and-event atomicity, receipt reference handling, and transaction rollback on an injected failure.

Do not mirror every private helper with a test that repeats its implementation. The useful tests assert externally meaningful state after a command and after a failure. For example, after an event-insert failure, the expense should retain its old status if the proposed transaction contract is all-or-nothing.

A database integration test should clean only its own isolated database or schema. The current course does not create one, install a provider, or run migrations. Its contribution is the explicit contract and fixture plan needed before such work is authorized and implemented.

## Independent exercises

Exercise NR20-A: inventory direct dictionary and shared-reference assumptions for Create, Update, Submit, Reject, receipt replacement, and reporting. Identify which would break with detached database values.

Exercise NR20-B: propose an aggregate schema and atomic transition save with version checking. Define missing, unauthorized, and stale-version outcomes without exposing unreviewed information.

Exercise NR20-C: design a synthetic migration dataset and adapter contract suite. Include one normalization conflict, one dangling reference, and one failure between status update and history append.
