# Verification and boundaries

Run these commands from the project root. The application targets net8.0; the recorded environment supplied the .NET 10 SDK/runtime, so tests explicitly enabled major-version roll-forward. A machine with the .NET 8 runtime can use its normal target runtime instead. The frontend was installed with Node22.16 and now includes a package lock for repeatable installation.

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet restore backend/ExpenseManagement.Api.Tests/ExpenseManagement.Api.Tests.csproj
dotnet test backend/ExpenseManagement.Api.Tests/ExpenseManagement.Api.Tests.csproj --no-restore --logger 'trx;LogFileName=expense.trx' --results-directory TestResults
dotnet build backend/ExpenseManagement.Api.csproj --no-restore -c Release
npm ci --prefix frontend
npm run build --prefix frontend
```

The [final test log](evidence/backend-tests-6.log) and [TRX counters](evidence/expense-tests.trx) record ten discovered, executed and passed tests with zero failures. One further test, `Discarded_WriteAsync_on_a_full_queue_parks_and_lands_after_a_dequeue`, was added to `ExpenseWorkflowTests.cs` after that evidence was captured, so a local `dotnet test` now reports **eleven passed, zero failed** (observed). The shipped TRX is not regenerated; treat it as the record of the original delivery, not as a claim about the current tree. The [backend Release build](evidence/root-backend-build-01.log) and [frontend production build](evidence/root-frontend-build-01.log) passed. The [results summary](evidence/results.json) links the authoritative check labels. Earlier failed compilation and smaller passing attempts may remain in evidence, but they are not counted as extra tests or the final result.

The suite invokes production ExpenseService and BackgroundJobs with real in-memory entities. It covers candidate validation/normalization, failed reject/update/submit state, rejected and throwing queues, repeated reimbursement commands, duplicate worker completion, the actual capacity-one channel and system-only transitions. The duplicate job has zero delay and does not force two threads to overlap. No live HTTP adapter, authenticated session, receipt file, payment rail, hosted worker or database was exercised. A successful frontend bundle does not establish browser interaction behavior.

The six changed baseline production files have exact original-byte snapshots. The new xUnit project is excluded from the parent Web SDK compile and content globs. The frontend package-lock.json is a new setup artifact; application frontend source is unchanged. Generated bin, obj and TestResults directories are local check output and are not delivered; the portable TRX above is included deliberately.

The dictionary and queue remain process-local and non-durable. Expense commands retrieve their current object under the same per-expense gate used by reimbursement, so a completed delete cannot leave those commands editing an earlier detached reference. No controlled delete-versus-submit interleaving test is included. Receipt upload, RefreshPolicy and mutable readers remain outside that guarantee. Lock entries are retained for the lifetime of the store, including deleted expense IDs. A database migration needs repository changes, a transaction/concurrency policy and durable work delivery; an in-memory lock cannot supply them. Queue failure leaves state unchanged because admission occurs before mutation, without a compensation write.
