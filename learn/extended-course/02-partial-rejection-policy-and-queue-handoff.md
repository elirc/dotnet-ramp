# 02. Partial rejection, policy evaluation, and queue handoff

Read [ExpenseService](../../backend/Services/ExpenseService.cs), [PolicyEngine](../../backend/Services/PolicyEngine.cs), [BackgroundTaskQueue](../../backend/Background/BackgroundTaskQueue.cs), and [BackgroundJobs](../../backend/Background/BackgroundJobs.cs). This chapter examines three boundaries that are easy to flatten into one word: validation, transition, and completion. The source does not make them one atomic operation.

## A rejected action can still change a field

Reject obtains a visible expense, checks that the actor manages its owner, assigns the trimmed rejection reason, and only then calls Transition to Rejected. If the current state has no permitted rejection edge, Transition throws after the reason assignment. A proposed test should capture the reason, status, timestamp, and history before and after to characterize that order.

Use an Approved expense belonging to Erin and a valid manager Morgan. Visibility and relationship checks pass. The new reason can be assigned, but Approved to Rejected is not in the table, so the transition rejects. The source suggests changed reason with unchanged status and no new transition history on that path. The [service characterization lab](13-service-characterization-and-independent-snapshots.md) now executes this path in copied current service code and verifies those field outcomes. The table lab alone does not prove the field mutation. The service lab uses real copied domain and policy classes, a controlled queue, and a declared job-factory stub; it does not run the HTTP host or real worker.

A stronger proposed contract might require every affected field unchanged on rejection. Achieving that contract requires preparing the reason and validating the transition before applying mutations, or another explicitly reviewed design. Merely catching the exception does not restore the old reason. Keep characterization of the existing order separate from the specification of a repair.

## Policy flags have severity and timing

PolicyEngine returns blocking flags for nonpositive amount, missing description, future date, and a missing receipt when amount exceeds twenty-five. It returns warnings for stale dates, an attached receipt still processing, category-limit excess, and high value. The comparisons matter: exactly twenty-five without a receipt does not trigger the greater-than threshold; exactly ninety days old does not satisfy the older-than condition.

An attached but unprocessed receipt produces a warning under the selected branch rather than the missing-receipt blocking flag. The code distinguishes receipt existence from processed status. A learner who assumes every unprocessed receipt blocks submission would predict the wrong behavior unless a separate guard exists elsewhere.

Construct a boundary table with amount twenty-five and twenty-five plus one cent, receipt absent, receipt attached and pending, and receipt attached and processed. Hold description and date valid so those guards do not contaminate the example. Then vary category and amount separately to distinguish warning accumulation from blocking status.

## Refresh does not mean immutable validation

RefreshPolicy replaces the expense's PolicyFlags list with the current evaluation. Submit calls it before checking blocking flags and before Transition. Therefore, policy evaluation is a visible mutation of the expense object, even when submission ultimately rejects. The method name refresh can make this seem incidental, but it matters to an all-fields-unchanged requirement.

The policy reads DateTime.UtcNow internally to derive today. A deterministic date-boundary lab would need a clock seam or a deliberately bounded fixture whose dates are derived safely. The current table lab avoids policy time entirely because it compiles only the enum and state-machine source. Do not claim that its exhaustive transition matrix tests date rules.

Warnings may coexist with a permitted submission because HasBlocking checks severity rather than any flag presence. An independent oracle should assert exact relevant codes and severity, not merely whether the list is empty. A test that rejects every flagged expense would encode a different policy from the selected implementation.

## Reimbursement request is not payment completion

StartReimbursement obtains a visible expense, transitions it to Reimbursing, calls EnqueueAsync with a generated job delegate, and returns the expense. The selected method does not await the returned ValueTask. The queue uses a bounded channel with FullMode.Wait, which can make enqueue completion asynchronous when capacity is unavailable.

The important distinction is between invoking enqueue and observing its completion. The source does not establish that the service waits for acceptance before returning. It also does not provide durable queue persistence. A process restart and a full queue are different failure scenarios and require separate experiments before making reliability claims.

Do not infer that every enqueue is lost or that the current UI always shows success too early. Those are runtime conclusions requiring execution. The source establishes the ordering and unobserved awaitable, which is enough to design a precise service-and-queue fixture using a gate rather than arbitrary sleeps.

## The worker has its own lifecycle

The hosted service dequeues with its stopping token, creates a fresh dependency-injection scope for each job, and awaits the job. Job exceptions are logged, and the loop continues. The selected code does not show a retry queue or durable failure record. Continuing the worker after one failure is different from retrying the failed business operation.

The reimbursement job delays with the token, looks up the expense, checks the legal edge to Reimbursed, assigns a simulated payment reference, status, timestamp, and history. Its reference is a local generated string, not evidence of an external payment provider charge. The comments explicitly describe simulated work, and the course retains that boundary.

A job can return because the expense is missing or because its state is no longer reimbursable. Those paths differ from successful completion and from an exception. A future status-reporting design should distinguish them if users need to understand why an expense remains in a particular state.

## A before-and-after worksheet

For each operation, record status, rejection reason, policy-code list, UpdatedAt, history count, queue-invocation count, and observed queue completion. Mark values as independent snapshots rather than keeping a reference to the same mutable list. For a worker experiment, add payment reference and job outcome.

Use separate rows for blocking submission, invalid-state rejection, valid reimbursement request with pending enqueue, worker cancellation during delay, and successful simulated completion. Do not label all failures rollback. Some happen before status mutation, some after another field changed, and some after a handoff was initiated.

The worksheet is a specification aid for later service labs. It does not require starting the application or contacting an external payment system. A carefully controlled fake queue and in-memory store can expose the relevant local ordering, while a durable design proposal would require additional persistence evidence.

## Independent exercises

Exercise NR02-A: predict the Approved-to-Rejected manager call with a different reason. Identify the first mutation and first rejection, then specify an all-fields-unchanged repair criterion without claiming it already holds.

Exercise NR02-B: complete the receipt threshold table and a warning-versus-blocking table. Explain why attached pending receipt and absent receipt are different branches, and why exact date boundaries need controlled time evidence.

Exercise NR02-C: draw request, enqueue invocation, enqueue completion, dequeue, and simulated payment completion as five events. Design a gate-based fixture that reveals the unawaited handoff and distinguish worker survival from retry reliability. Read the separate solutions after writing predictions.
