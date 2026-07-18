# Performance Thinking

**Rule zero: measure before optimizing.** This app is tiny and in-memory; nothing here needs
optimizing today. The value is learning *where to look* and *what will bite at scale*.

## Performance domains and where they live

| Domain | Where in this repo | Today | At scale |
| --- | --- | --- | --- |
| Server compute | `ExpenseService.List`, `ReportingService` | trivial | LINQ over a growing in-memory collection becomes O(n) per request |
| DB | none yet | n/a | the N+1 below appears the moment you add EF Core |
| Background | `QueuedHostedService` (single consumer) | fine | one worker = serial throughput ceiling |
| Network | SPA `fetch` + 1.5s polling | fine | polling N open detail pages = N req/1.5s |
| Bundle/startup | Vite build | fine | watch bundle size as deps grow |
| Allocations/GC | LINQ in `List`/mapping | negligible | hot paths if collections grow large |

## The N+1 you can already see (latent)

[`Mappings.ToResponse` L19](../../../backend/Services/Mappings.cs#L19) does
`store.Users.GetValueOrDefault(e.EmployeeId)` **per expense**. Mapping a list calls it once
per row. In memory: free. Under EF Core with lazy/per-row loading: **N queries for N
expenses** — the single most common .NET performance bug.

**How you'd find it:** turn on EF Core query logging and watch the console fill with
identical `SELECT * FROM Users WHERE Id = @p` lines while loading one list.

**The fix (when relevant):** batch-load the needed users once (`WHERE Id IN (…)`) or project
the join in the query, then map from the in-memory dictionary. Recognize the shape: *a lookup
inside a per-item map/loop is a future N+1.*

## Serial awaits (a shape to recognize)

The worker processes jobs one at a time
([`QueuedHostedService` L28-49](../../../backend/Background/QueuedHostedService.cs#L28-L49)).
That's deliberate and safe here. The transferable lesson: a loop of `await`s that are
*independent* can sometimes be parallelized (`Task.WhenAll`) — but only with bounded
concurrency and care for shared state (see [Kata 8](../04-code-reading-gym/04-review-katas.md)).
Don't parallelize without a reason and a bound.

## Polling vs. push (frontend)

[`ExpenseDetailPage` L26-32](../../../frontend/src/pages/ExpenseDetailPage.jsx#L26-L32) polls
every 1.5s while work is pending. Simple and fine for a demo. At scale you'd prefer
server push (SignalR/WebSocket/SSE) so the client learns *when* the job finishes instead of
asking repeatedly. Trade-off: push adds connection management; polling wastes requests.

## A measurement checklist (transferable)

1. Reproduce with a realistic data size — perf bugs hide at N=2.
2. Measure one thing (a stopwatch, a profiler, the EF query log) — don't guess.
3. Find the dominant cost (usually I/O or a query, rarely CPU).
4. Fix the dominant cost; re-measure.
5. Stop when it's good enough — over-optimizing tiny code is its own waste.

**Drill:** Without changing code, write down the three lines you'd watch first if the list
endpoint got slow after a hypothetical EF Core migration. (Answer: the `ToList` in `List`,
the per-row lookup in `Mappings`, and whether the query filters in SQL or memory.)
