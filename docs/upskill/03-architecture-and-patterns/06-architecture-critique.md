# Architecture Critique

A senior read of the design: what's strong, what's risky, and what I'd do if I owned it for
three months. Each claim is labelled **confirmed** (read in the code) or **hypothesis**
(needs measurement/runtime).

## Strongest choices (keep these)

1. **State changes funnel through one state machine.** *(confirmed —
   [`Transition` L169-178](../../../backend/Services/ExpenseService.cs#L169-L178),
   [`ExpenseStateMachine`](../../../backend/Domain/ExpenseStateMachine.cs))* This is the
   spine of the domain and the reason the lifecycle is trustworthy.
2. **Authorization in the service layer, with resource scoping.** *(confirmed — L181-205)*
   The IDOR defense is real, not just role attributes.
3. **Clean layering with a DTO boundary.** *(confirmed —
   [`Mappings.cs`](../../../backend/Services/Mappings.cs))* Secrets can't leak; the wire
   contract is decoupled from the model.
4. **Zero-dependency, framework-native implementation.** *(confirmed —
   [csproj](../../../backend/ExpenseManagement.Api.csproj))* Excellent for learning and for
   supply-chain surface; everything is inspectable.
5. **Correct background-work idioms** (scope-per-job, error isolation, backpressure).
   *(confirmed — [`QueuedHostedService`](../../../backend/Background/QueuedHostedService.cs))*

## Risks & trade-offs

| # | Risk | Evidence | Impact | Confidence |
| --- | --- | --- | --- | --- |
| R1 | Durability: queue + data are in-memory; crash loses everything, incl. in-flight reimbursements | [`StartReimbursement` L153-154](../../../backend/Services/ExpenseService.cs#L153-L154) | Stuck `Reimbursing`, lost work | confirmed |
| R2 | Concurrency: singleton store + mutable entities, no locks | [`DataStore`](../../../backend/Storage/DataStore.cs), [`ProcessReimbursement` L58-66](../../../backend/Background/BackgroundJobs.cs#L58-L66) | Data races, torn reads | confirmed (severity = hypothesis) |
| R3 | No tests anywhere | repo has no test project | Regressions invisible; refactors risky | confirmed |
| R4 | TOCTOU on submit (policy check then transition, not atomic) | [`Submit` L117-128](../../../backend/Services/ExpenseService.cs#L117-L128) | Rare double-submit | confirmed |
| R5 | Content type trusted from client; bytes not sniffed | [`ReceiptStorage` L45](../../../backend/Services/ReceiptStorage.cs#L45) | Mislabeled/abusive uploads | confirmed |
| R6 | Session memory grows unbounded (lazy prune only) | [`AuthService` L43-52](../../../backend/Security/AuthService.cs#L43-L52) | Slow leak | confirmed |
| R7 | N+1 lookups in mapping if store becomes a real DB | [`Mappings` L19](../../../backend/Services/Mappings.cs#L19) | Latency at scale | hypothesis (only under EF Core) |
| R8 | Config typo for CORS origins silently falls back to localhost | [`Program.cs` L37-38](../../../backend/Program.cs#L37-L38) | Wrong-origin in prod | confirmed |

Full register with suggested tests/fixes: [08/risk-register](../08-reference/risk-register.md).

## Prioritized improvements (impact × effort)

| Priority | Change | Why first | Rough effort |
| --- | --- | --- | --- |
| P0 | Add a test project; cover the state machine, policy engine, and the IDOR path | Unlocks safe change everywhere | S |
| P0 | Make reimbursement crash-safe (recover stuck `Reimbursing`, or outbox) | Money path; current gap is real | M |
| P1 | Decide concurrency story (document single-writer assumption *or* add locking/EF Core) | Correctness under load | M |
| P1 | Sniff/validate uploaded file bytes; cap by real type | Security | S |
| P2 | Persist data (EF Core + SQLite) behind the storage layer | Durability; realistic next step | M–L |
| P2 | Observability: metrics + health checks + structured logs | "How would I know it broke?" | M |

## "If I owned this for three months"

- **Weeks 1–2:** stand up CI + a test project; pin the SDK (`global.json`); write
  characterization tests for the state machine, policy engine, and authorization. This buys
  the freedom to change everything else.
- **Weeks 3–6:** introduce EF Core + SQLite behind `DataStore` (now an interface); migrate
  reads/writes; keep the API contract identical so the SPA never notices. Add the N+1 fix
  (batch employee lookups in `Mappings`) as part of this.
- **Weeks 7–9:** make the money path durable — outbox for reimbursement, retries +
  dead-letter for the worker, a "stuck job" detector + alert.
- **Weeks 10–12:** observability (OpenTelemetry traces, queue-depth + job-duration metrics,
  health checks), and harden uploads (byte sniffing, AV hook). Then revisit auth (JWT, token
  refresh, session pruning).

Each of these has a migration + test + rollback note in
[06/03 senior projects](../06-contribution-practice/03-senior-build-projects.md).

## Migration & test strategy per recommendation (sketch)

- **EF Core swap:** turn `DataStore` into `IDataStore`; implement an EF version; switch the
  DI registration. Test: run the *same* service tests against both implementations (a
  contract test). Rollback: flip the registration back.
- **Outbox:** add an `OutboxEntry` written in the same `SaveChanges` as the status change; a
  dispatcher polls it. Test: kill the dispatcher mid-flight, restart, assert exactly-once
  payment. Rollback: feature-flag the dispatcher; fall back to direct enqueue.
- **Concurrency:** start by *documenting* the single-writer assumption and adding an
  optimistic-concurrency token to `Expense`; reject stale writes. Test: two concurrent
  submits → one 409. Rollback: ignore the token.
