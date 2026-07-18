# Risk Register

Confidence: **confirmed** (read in code) · **hypothesis** (needs runtime/measurement). Most
risks are acceptable *for a learning app* — they're listed because recognizing them is the
skill, and they become real on the path to production.

| ID | Risk | Evidence (anchor) | Impact | Likelihood | Suggested test | Suggested fix | Confidence |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R1 | Crash loses all data + in-flight jobs (in-memory store & queue) | [`DataStore`](../../../backend/Storage/DataStore.cs), [`BackgroundTaskQueue`](../../../backend/Background/BackgroundTaskQueue.cs) | High | High in prod | restart mid-flow, assert loss | EF Core + outbox ([Projects 1–2](../06-contribution-practice/03-senior-build-projects.md)) | confirmed |
| R2 | Stuck `Reimbursing` on crash; no retry | [`StartReimbursement` L153-154](../../../backend/Services/ExpenseService.cs#L153-L154) | High (money) | Med | kill before job runs | outbox + stuck-detector | confirmed |
| R3 | Data race: singleton store + mutable entities, no locks | [`ProcessReimbursement` L58-66](../../../backend/Background/BackgroundJobs.cs#L58-L66) | Med | Low at demo scale | concurrent submit+read | optimistic concurrency / EF Core | confirmed (severity hypothesis) |
| R4 | No automated tests | repo-wide | High (regressions) | High | n/a | test project + CI ([Project 4](../06-contribution-practice/03-senior-build-projects.md)) | confirmed |
| R5 | TOCTOU: policy check then transition not atomic | [`Submit` L117-128](../../../backend/Services/ExpenseService.cs#L117-L128) | Low | Low | two concurrent submits | concurrency token / lock | confirmed |
| R6 | Upload content type trusted from client; no byte sniffing/AV | [`ReceiptStorage` L45](../../../backend/Services/ReceiptStorage.cs#L45) | Med | Med | upload mislabeled file | sniff magic bytes; AV hook | confirmed |
| R7 | Session memory grows unbounded (lazy prune only) | [`AuthService` L43-52](../../../backend/Security/AuthService.cs#L43-L52) | Low | Med (long uptime) | create many sessions, observe | periodic sweep / stateless JWT | confirmed |
| R8 | N+1 employee lookups once a real DB exists | [`Mappings` L19](../../../backend/Services/Mappings.cs#L19) | Med | High (post-EF) | EF query log on list | batch-load / join | hypothesis (EF only) |
| R9 | CORS origins silently fall back to localhost on config typo | [`Program.cs` L37-38](../../../backend/Program.cs#L37-L38) | Med | Low | bad config, observe origin | fail fast on missing config | confirmed |
| R10 | Token in `localStorage` is XSS-readable | [`client.js` L6-10](../../../frontend/src/api/client.js#L6-L10) | Med | Low (no XSS sink found) | XSS audit | httpOnly cookie option | confirmed |
| R11 | No rate limiting on login (brute force, slowed only by PBKDF2) | [`AuthController`](../../../backend/Controllers/AuthController.cs) | Med | Med | hammer login | lockout / rate limiter ([Ticket 10](../06-contribution-practice/02-mid-level-feature-tickets.md)) | confirmed |
| R12 | Inconsistent idempotency (reimburse guarded, receipt not) | [`BackgroundJobs`](../../../backend/Background/BackgroundJobs.cs) | Low | Low | run receipt job twice | add guard ([Ticket 4](../06-contribution-practice/01-good-first-tickets.md)) | confirmed |
| R13 | Single instance only (no shared state) | [`Program.cs` L16-20](../../../backend/Program.cs#L16-L20) | Med | n/a until scaling | run 2 instances | shared DB + queue ([Project 6](../06-contribution-practice/03-senior-build-projects.md)) | confirmed |

## How to use this
- **Learners:** pick a row, confirm the evidence yourself, then write the failing test that
  would prove it (where applicable). That's the highest-leverage exercise in the suite.
- **Reviewers:** these are the standing concerns; a PR that worsens one should say so.
