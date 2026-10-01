# 04: Testing and debugging the boundaries

The test project is `backend/ExpenseManagement.Api.Tests/ExpenseManagement.Api.Tests.csproj`. It references the application project and uses xUnit. Restore before testing, because a no-restore run can succeed without discovering the intended test assets. The reproducible command from that directory is:

```powershell
$env:DOTNET_ROLL_FORWARD = 'Major'
dotnet restore ExpenseManagement.Api.Tests.csproj
dotnet test ExpenseManagement.Api.Tests.csproj --no-restore --logger 'trx;LogFileName=expense.trx' --results-directory TestResults
```

The suite has eleven facts. Three cover failure-before-mutation for reject, update, and submit. Two cover full-queue and throwing-queue reimbursement. One invokes the actual reimbursement delegate twice, one checks the state machine’s worker authority, one checks positive create/update normalization, one exercises the actual capacity-one queue, one covers the invalid-field matrix, and one (`Discarded_WriteAsync_on_a_full_queue_parks_and_lands_after_a_dequeue`, added after the original delivery) pins down what `BoundedChannelFullMode.Wait` really does when the channel is full — see chapter 02. The test fixture creates real `DataStore` users and real `Expense` objects. The queue is a controllable boundary double where needed; the service and worker remain production types.

When debugging a state leak, capture independent values before the call. `ExpenseWorkflowTests` uses a formatted snapshot string for update and queue cases, and copies policy flags and history into arrays for submit. Do not store `var before = expense` because that is another reference to the same mutable object. Also assert a positive result where an `Any` or `All` assertion could pass on an empty collection: the reimbursement test requires exactly two history entries and exactly one `Reimbursed` event.

A useful investigation sequence is: identify the public entry point, record all mutable fields, force the failure branch, and compare every field that the successful branch would touch. For a reject failure, status, reason, history, and `UpdatedAt` are the minimum. For submit, include policy flags because policy refresh is itself a mutation. For queue admission, include the status and event count, then inspect the queue’s captured work count.

TRX is the evidence format to inspect. Find `ResultSummary` and read `Counters total`, `executed`, `passed`, and `failed`; do not infer the count from `[Fact]` annotations. The delivered evidence in `evidence/` records ten executed and ten passed; that TRX is the original delivery. The eleventh fact was added later, so a local run now reports eleven passed and the shipped TRX will not match your terminal. That mismatch is deliberate and disclosed rather than silently regenerated: when a number in a course and a number on your screen disagree, find out which artifact is older before you assume either is wrong. Earlier test attempts are retained as a failed compile log and smaller passing runs for honest history, while `backend-tests-6` is the final run. The final results object points to the passing command and real TRX file.

If a test hangs, look first for a queue operation that waits while holding an expense lock. Reimbursement must call `TryEnqueue`, not `EnqueueAsync`, and no worker should synchronously wait on an async operation under the lock. If a duplicate completion appears, verify that the worker’s status check and payment mutation are inside the same gate.
