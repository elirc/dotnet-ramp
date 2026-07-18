# Observability & Operations

> "How would I know this broke?" — ask it of every flow. Today, for most flows, the honest
> answer is *you wouldn't, until a user complains.* This page is about closing that gap.

## What exists today

- **Structured logging via `ILogger`** in the background worker and jobs:
  worker start/stop ([`QueuedHostedService` L26,51](../../../backend/Background/QueuedHostedService.cs#L26)),
  job failures ([L47](../../../backend/Background/QueuedHostedService.cs#L47)), receipt/reimbursement
  events ([`BackgroundJobs` L32,53,68](../../../backend/Background/BackgroundJobs.cs#L32)).
  Note the good use of **structured** placeholders (`{ReceiptId}`) not string concatenation.
- **Centralized error logging** for 500s in the middleware
  ([L45-46](../../../backend/Middleware/ExceptionHandlingMiddleware.cs#L45-L46)).

## What's missing (and why it matters per flow)

| Flow | How you'd know it broke today | What you'd add |
| --- | --- | --- |
| Login | User reports they can't log in | Counter of login failures; alert on spike (brute force) |
| Submit | User reports a 422 | Fine — the error is actionable; maybe a metric of block reasons |
| Receipt processing | Nothing — silently stuck `Pending` | Metric: receipts pending > N minutes → alert |
| Reimbursement | Nothing — silently stuck `Reimbursing` | Metric + alert on stuck money path (highest priority) |
| Worker health | Nothing if the worker thread dies | Health check + queue-depth metric |

The reimbursement gap is the one that would actually cost money — flagged in
[03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md) and the
[risk register](../08-reference/risk-register.md).

## A pragmatic observability roadmap (no over-engineering)

1. **Health checks** — `builder.Services.AddHealthChecks(); app.MapHealthChecks("/health");`
   (built-in, zero packages). Add a check that the worker is alive and the queue isn't
   wedged. *This is the cheapest high-value add.*
2. **A few metrics** — `System.Diagnostics.Metrics` (built-in): queue depth, job duration,
   job failures, reimbursements completed. Export later via OpenTelemetry if needed.
3. **Correlation** — include a request id / expense id in logs so you can follow one
   reimbursement end to end. (The jobs already log the entity id — good start.)
4. **A "stuck" detector** — a periodic `BackgroundService` that scans for `Reimbursing`
   older than N minutes and logs/alerts.

## Operating it (today's reality)

| Concern | Reality |
| --- | --- |
| Deploy | `dotnet run` / `dotnet publish`; SPA via `vite build` |
| Rollback | Redeploy previous build. **Data is in-memory → a restart wipes everything.** |
| Local vs prod | Identical except config; *no* durable state in either |
| Scaling | Single instance only — a second instance would have its own separate in-memory store and queue (no shared state) |

**The operational headline:** because state is in-memory, this app **cannot be horizontally
scaled or safely restarted** without data loss. That's acceptable for learning and is the
clearest argument for the EF Core migration in [03/02](../03-architecture-and-patterns/02-data-model-and-persistence.md).

**Drill:** Pick the reimbursement flow. Write the exact health-check + metric + alert you'd
add so that "stuck in Reimbursing" pages someone within 5 minutes. Name the files you'd touch.
