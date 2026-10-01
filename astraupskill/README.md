# Expense workflow upskill lab

This nine chapter lab uses the ASP.NET Core expense application in this folder as its teaching source. The application has employees create and submit expenses, managers approve or reject direct reports, and finance start reimbursement. A process local bounded queue finishes reimbursement in a worker. The important lesson is that CRUD code is also stateful business code: a request can touch fields, policy flags, audit history, and an asynchronous job, so a failure boundary must cover all of those effects.

Start by restoring the focused project from `backend/ExpenseManagement.Api.Tests`:

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet restore ExpenseManagement.Api.Tests.csproj
dotnet test ExpenseManagement.Api.Tests.csproj --no-restore --logger 'trx;LogFileName=expense.trx'
```

The tests instantiate the production `ExpenseService`, `BackgroundJobs`, `DataStore`, policy engine, and queue contract. They do not create a second fake workflow. Read `backend/Services/ExpenseService.cs` beside `backend/Domain/ExpenseStateMachine.cs`, then follow the queue into `backend/Background/BackgroundJobs.cs`. The course moves from map, to concepts, to one worked repair, then to test design, practice, review, and a request trace. `VERIFICATION.md` records the finite command and the ten-test TRX counters from the original delivery; a local run today reports eleven, because one test was added afterwards (see chapter 04).

The repair has four observable guarantees. A malformed update is rejected before the live entity changes. A rejection on an illegal state leaves reason, history, and timestamp untouched. A policy-blocked submit leaves the previous policy list untouched. Reimbursement uses `TryEnqueue` while holding a per-expense gate; a full queue therefore leaves an approved expense approved. The worker uses the same gate and an explicitly system-only state-machine edge, making duplicate jobs harmless.

When you extend the lab, keep the application boundary in mind. Data annotations protect HTTP model binding, but callers such as a scheduled job or a unit test can call the service directly. `ValidateAndNormalize` is therefore the service’s second line of defense. The in-memory dictionaries make individual lookup operations safe, but they do not make a sequence of entity mutations atomic. `DataStore.GetExpenseLock` supplies the missing per-expense coordination while leaving unrelated expenses concurrent.

Practice answers are intentionally tied to files that exist here. Copy a snippet only after opening the referenced file, and run the real test command after each change. The queue remains process local and no payment rail or durable database is introduced by this lab.

## Open the learning sequence

Follow the [map](01-CODEBASE-MAP.md), [concepts](02-CONCEPTS.md), [worked change](03-WORKED-CHANGE.md), [testing](04-TESTING-AND-DEBUGGING.md), [practice](05-PRACTICE.md), [answers and complete fixture](06-SOLUTIONS-AND-REVIEW.md), [trace](07-TRACE-LAB.md), and [verification](VERIFICATION.md). Compare the [expense service](../backend/Services/ExpenseService.cs), [queue](../backend/Background/BackgroundTaskQueue.cs), [worker](../backend/Background/BackgroundJobs.cs), and [actual tests](../backend/ExpenseManagement.Api.Tests/ExpenseWorkflowTests.cs). The final evidence includes both backend and frontend builds.

## Before you start

**Prerequisites.** C# classes, `async`/`await`, and the shape of an ASP.NET Core controller. You should have written at least one xUnit test before. You do **not** need to know `System.Threading.Channels`, background services, or state machines — chapters 01 and 02 introduce all three. If you have never seen a `lock` statement, read chapter 02's concurrency section twice; everything else in the lab rests on it.

**Toolchain.** The application targets `net8.0`; if your machine has a newer SDK (this one had .NET 10), set `DOTNET_ROLL_FORWARD=Major` as the commands above do. Node is only needed for the frontend build, which this lab does not require. On macOS or Linux the commands are identical apart from the environment-variable syntax: `DOTNET_ROLL_FORWARD=Major dotnet test ...`.

## Reading route and time estimates

| Chapter | What you get | Estimate |
| --- | --- | --- |
| [01 Codebase map](01-CODEBASE-MAP.md) | The five commands as a table: route → action → service → role gate → what is written → queued job | 40–50 min |
| [02 Concepts](02-CONCEPTS.md) | Validation vs. authorization, failure-before-mutation, what a bounded channel does when full, and the database bridge | 60–80 min |
| **Exercise 0** in [05](05-PRACTICE.md) | You put the three defects back and watch four tests go red | 45 min |
| [03 Worked change](03-WORKED-CHANGE.md) | The real repair, and a precise account of what the original bug was and was not | 45 min |
| [04 Testing and debugging](04-TESTING-AND-DEBUGGING.md) | How the suite is built and how to read a TRX | 40 min |
| [05 Practice](05-PRACTICE.md) exercises 1–4 | Four focused test-writing exercises | 1 session each |
| [05 Practice](05-PRACTICE.md) exercises 5–6 | Contract design, then the EF Core / outbox bridge | 1 session, then 2–3 |
| [06 Solutions and review](06-SOLUTIONS-AND-REVIEW.md) | Answers and the full delivered fixture — after you have tried | 45 min |
| [07 Trace lab](07-TRACE-LAB.md) | One reimbursement followed step by step | 30 min |
| [VERIFICATION](VERIFICATION.md) | Commands, evidence and the disclosed limits | 15 min |

Chapters 01–04 plus Exercise 0 are one full day. "One session" is two to three focused hours.

## Glossary

- **Command boundary** — the span of code inside which a request's effects either all happen or none do. Here it is one `lock (_store.GetExpenseLock(id))`; in a database it is one transaction.
- **Admission** — the moment a queue accepts responsibility for a job. `TryEnqueue` returns the admission decision immediately; `EnqueueAsync` on a full `Wait` channel defers it.
- **Back-pressure** — a full queue making the producer slow down instead of accepting unbounded work. Discarding the `ValueTask` from `EnqueueAsync` throws back-pressure away.
- **`BoundedChannelFullMode`** — the option that decides what a writer experiences on a full channel: `Wait` (park until there is room — what this app uses), `DropWrite`, `DropOldest`, `DropNewest`. Chapter 02 has the full table. Guessing this wrong is the most common `Channels` mistake.
- **Fire-and-forget** — calling an async method without awaiting it. The work may still happen; the *failure* certainly will not be reported.
- **Gate / per-entity lock** — an object used purely as a `lock` target so that all code touching one expense serialises. `DataStore.GetExpenseLock(id)` hands out one per expense ID.
- **Legal edge** — an allowed transition in `ExpenseStateMachine.Transitions`, e.g. `Submitted -> Approved`. Each edge carries an array of roles permitted to take it.
- **System-only edge** — an edge whose role array is empty (`Reimbursing -> Reimbursed`). Only the background worker may take it; `CanSystemTransition` enforces exactly this.
- **Failure-before-mutation** — validate and authorize completely before changing any field of a live entity, so a rejected request leaves nothing behind.
- **Live entity** — the `Expense` object stored in the dictionary. Everything that holds a reference sees your changes immediately, including mid-command.
- **Transactional outbox** — writing a "please do this job" row in the same transaction as the state change, so the job cannot be lost to a crash. The thing `TryEnqueue` cannot give you; exercise 6.
- **`ValueTask`** — a struct-based task. It may be awaited only **once**, and must not be stored, ignored and re-inspected, or passed to `Task.WhenAll` without `.AsTask()`.
