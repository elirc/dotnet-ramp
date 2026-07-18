# Senior Build Projects

System-level changes. Each is realistic (not a rewrite) and comes with the plans a senior is
expected to produce. Template: **Problem · Value · Design decisions · Files/modules ·
Migration · Tests · Security · Performance · Rollout/Rollback · Open questions · Stretch.**

---

## Project 1 — Durable persistence (EF Core + SQLite) behind the storage seam

**Problem:** all data is in-memory; restart = total loss; can't scale.
**Value:** durability and a realistic data layer.
**Design decisions:** introduce `IDataStore` (Ticket 12) first; implement an EF Core version;
`DbContext` is **scoped** (not singleton like today's store); keep the API contract identical.
**Files:** `Storage/`, `Program.cs` (registration + lifetime change), service constructors,
new `Migrations/`.
**Migration:** add the context + initial migration; seed via the existing `DataSeeder` logic.
**Tests:** run the *same* service-level tests against both `IDataStore` implementations (a
contract test) so behavior is provably unchanged.
**Security:** parameterized by default (LINQ→SQL); move any connection string to user-secrets.
**Performance:** fix the latent N+1 in `Mappings` ([L19](../../../backend/Services/Mappings.cs#L19))
as part of this — batch user lookups or project a join.
**Rollout/Rollback:** flip DI registration; rollback = register the in-memory impl.
**Open questions:** SQLite vs Postgres? How to model `History` (child table vs JSON)?
**Stretch:** optimistic concurrency token on `Expense` to close the TOCTOU window.

---

## Project 2 — Reliable reimbursement (outbox + retries)

**Problem:** the money path can get stuck in `Reimbursing` on a crash, with no retry
([03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md)).
**Value:** no lost/duplicated payments.
**Design decisions:** write the status change **and** an outbox record in one transaction
(requires Project 1); a dispatcher reads the outbox, runs the side effect with retry/backoff,
marks it done; rely on the existing idempotency guard.
**Files:** `Background/`, `Storage/`, the reimbursement path in `ExpenseService`.
**Migration:** add an `Outbox` table; backfill not needed (forward-only).
**Tests:** kill the dispatcher mid-flight, restart → exactly-once payment; poison job → dead-letter.
**Security:** payment provider key in a secret store.
**Performance:** dispatcher batch size; avoid hot-looping an empty outbox.
**Rollout/Rollback:** feature-flag the dispatcher; fall back to today's direct enqueue.
**Open questions:** polling vs. notification dispatcher? Dead-letter handling UX?
**Stretch:** generalize the outbox for notifications too.

---

## Project 3 — Real authentication (JWT + refresh + pruning)

**Problem:** opaque in-memory sessions don't survive restart, can't scale across instances,
never get pruned.
**Value:** stateless auth, horizontal scaling, proper expiry.
**Design decisions:** issue short-lived signed JWTs + a refresh token; keep the
`TokenAuthenticationHandler` *surface* (or switch to JwtBearer — note: that's a NuGet
package, a deliberate dependency decision to call out).
**Files:** `Security/`, `Program.cs`, frontend token handling.
**Migration:** dual-accept old sessions + new JWTs during transition.
**Tests:** expired token → 401; refresh flow; role claims preserved.
**Security:** signing-key management; refresh-token rotation; `localStorage` vs httpOnly cookie trade-off.
**Performance:** no per-request session lookup (stateless).
**Rollout/Rollback:** dual-accept window; rollback = disable JWT issuance.
**Open questions:** where to store the signing key? Cookie vs header for the SPA?
**Stretch:** sliding sessions / device management.

---

## Project 4 — Test + CI foundation

**Problem:** no tests, no CI; every change is risky.
**Value:** confidence to change anything else (prerequisite for Projects 1–3).
**Design decisions:** xUnit project; characterization tests for state machine, policy,
authorization; `WebApplicationFactory` smoke tests; a GitHub Actions workflow building +
testing both backend and frontend on PR.
**Files:** `backend.Tests/`, `.github/workflows/ci.yml`, `Program.cs` (test seam).
**Tests:** the recipes in [05/02](../05-quality-engineering/02-writing-tests-here.md).
**Rollout/Rollback:** purely additive.
**Open questions:** coverage gate threshold? Frontend test framework (RTL/Vitest)?
**Stretch:** add a clock seam so date-policy tests are deterministic.

---

## Project 5 — Observability for the money path

**Problem:** no way to know reimbursement is stuck until a user complains
([05/06](../05-quality-engineering/06-observability-and-operations.md)).
**Value:** detect failures in minutes, not days.
**Design decisions:** health checks; `System.Diagnostics.Metrics` for queue depth / job
duration / failures / reimbursements; a periodic "stuck `Reimbursing`" detector that alerts.
**Files:** `Background/`, `Program.cs`, a new monitor service.
**Tests:** the detector flags an artificially-stuck expense.
**Performance:** metric cardinality kept low.
**Rollout/Rollback:** additive; rollback = remove endpoints/metrics.
**Open questions:** export to where (Prometheus/OTLP)? Alert routing?
**Stretch:** distributed tracing across request → job.

---

## Project 6 — Horizontal scalability

**Problem:** in-memory store + in-process queue mean exactly one instance.
**Value:** run N instances behind a load balancer.
**Design decisions:** depends on Projects 1 (shared DB) + 2 (shared/durable queue) + 3
(stateless auth). This project is mostly *sequencing* the others + a shared queue (e.g. a DB
queue or a broker).
**Files:** infra config; queue implementation swap.
**Migration:** roll instances behind the LB once shared state exists.
**Tests:** two instances process the same queue without double-work (idempotency holds).
**Rollout/Rollback:** scale to 1 to roll back.
**Open questions:** broker choice; sticky sessions needed? (No, once auth is stateless.)
**Stretch:** zero-downtime deploys.

---

**How to use these:** pick one, write the RFC ([07/02](../07-career-and-collaboration/02-writing-prs-and-rfcs.md))
*before* coding, and get the design reviewed. The plan is the deliverable as much as the code.
