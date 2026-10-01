# .NET Runtime Model (through this repo)

## async / await

**Concept:** `async` methods return a `Task`/`ValueTask` and yield the thread at each
`await` instead of blocking it. The thread is returned to the pool to serve other requests.

**Real code:** [`ReceiptStorage.SaveAsync` L37-72](../../../backend/Services/ReceiptStorage.cs#L37-L72)
awaits the disk write (`await file.CopyToAsync(...)`), so the request thread isn't held
during I/O. The whole call chain is async up to the controller
([`ReceiptsController.Upload` L25](../../../backend/Controllers/ReceiptsController.cs#L25)).

**Why it matters:** a server that blocks a thread per in-flight I/O runs out of threads
under load. Async I/O is how a handful of threads serve thousands of concurrent requests.

**Failure mode — sync-over-async:** calling `.Result` or `.Wait()` on a Task blocks the
thread and can deadlock or exhaust the pool. This repo correctly avoids it: there is no
`.Result`/`.Wait()` anywhere. The one shortcut to notice is the **discarded `ValueTask`** at
[`ExpenseService.StartReimbursement` L154](../../../backend/Services/ExpenseService.cs#L154) —
`_queue.EnqueueAsync(...)` is called without `await`. It compiles (the method is
synchronous), but the enqueue's backpressure is ignored. Compare with
[`ReceiptService.UploadAsync` L52](../../../backend/Services/ReceiptService.cs#L52) which
*does* await it.

**Drill:** Why can `StartReimbursement` get away with not awaiting, while a real outbox
would have to? (Hint: the queue is in-memory and writes complete synchronously when there's
capacity — see `BoundedChannelOptions` at [L22-25](../../../backend/Background/BackgroundTaskQueue.cs#L22-L25).)

## Task vs ValueTask

**Concept:** `Task` is a heap-allocated object; `ValueTask` is a struct that avoids the
allocation when the result is already available (a hot path that often completes synchronously).

**Real code:** the queue uses `ValueTask` for `EnqueueAsync`/`DequeueAsync`
([`IBackgroundTaskQueue` L11-12](../../../backend/Background/BackgroundTaskQueue.cs#L11-L12))
because writes/reads frequently complete without suspension. The jobs themselves return
`Task` ([`BackgroundJobs` L16](../../../backend/Background/BackgroundJobs.cs#L16)) because
they always do real async work (a delay).

**Failure mode:** a `ValueTask` must not be awaited twice or consumed after its underlying
operation completed. Rule of thumb: `await` it exactly once, immediately. This repo follows
that.

## CancellationToken

**Concept:** cooperative cancellation — long operations accept a token and bail when it's
signalled.

**Real code:** the token threads from the worker through the jobs:
[`QueuedHostedService.ExecuteAsync(CancellationToken stoppingToken)` L24](../../../backend/Background/QueuedHostedService.cs#L24)
→ `DequeueAsync(stoppingToken)` → `await Task.Delay(..., ct)` at
[`BackgroundJobs` L22](../../../backend/Background/BackgroundJobs.cs#L22). On shutdown the
delay throws `OperationCanceledException`, caught at
[`QueuedHostedService` L35-38](../../../backend/Background/QueuedHostedService.cs#L35-L38).

**Failure mode:** ignoring the token means graceful shutdown hangs until in-flight work
finishes (or the host force-kills it). Note the upload path passes `ct` from the request
([`ReceiptsController` L25](../../../backend/Controllers/ReceiptsController.cs#L25)) — if the
client disconnects mid-upload, the copy can be cancelled.

## Threads & shared state

**Concept:** the background worker runs on a **different thread** from request handlers, but
both touch the same singleton `DataStore`.

**Real code:** `DataStore` uses `ConcurrentDictionary`
([`DataStore.cs`](../../../backend/Storage/DataStore.cs)) so individual put/get/remove are
thread-safe. But the *entities inside* are plain mutable objects. When
[`ProcessReimbursement` L58-66](../../../backend/Background/BackgroundJobs.cs#L58-L66) mutates
an `Expense` while a request handler reads the same object, there's no lock.

**Failure mode:** a read could see a half-updated entity (e.g. status changed but
`PaymentReference` not yet set). For a learning app with seconds-apart actions this is
practically invisible; at scale it's a real data race. Logged in the
[risk register](../08-reference/risk-register.md). **Senior lesson:** "thread-safe
collection" ≠ "thread-safe data" — the container and its contents are different guarantees.

## IDisposable / using

**Concept:** `using` deterministically releases unmanaged resources (file handles, scopes).

**Real code:** `await using (var stream = new FileStream(...))` at
[`ReceiptStorage` L59-62](../../../backend/Services/ReceiptStorage.cs#L59-L62) closes the file
handle even if the copy throws. `using var scope = _services.CreateScope()` at
[`QueuedHostedService` L42](../../../backend/Background/QueuedHostedService.cs#L42) disposes
the per-job DI scope (and any scoped services in it) when the job finishes.

**Failure mode:** forgetting `using` on a `FileStream` leaks handles; forgetting to scope
background work and instead capturing a request-scoped service causes the "captive
dependency" bug (covered in [02/02](02-framework-mental-models.md)).

## GC & allocations (lightly, since this app is small)

**Concept:** the GC reclaims unreferenced heap objects; fewer allocations = less GC pressure.

**Where to look:** the hot path here is `ExpenseService.List` →
[L30-52](../../../backend/Services/ExpenseService.cs#L30-L52), which allocates LINQ iterators
and a final `List`. Negligible at this scale, but it's the right place to *think* about
allocations if the store ever grew. **Senior lesson:** don't micro-optimize allocations
until a profiler says to — but know where your hot loops are.
