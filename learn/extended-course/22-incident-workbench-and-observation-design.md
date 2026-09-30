# 22. Incident workbench and observation design

Use [ExpenseService](../../backend/Services/ExpenseService.cs), [ReceiptService](../../backend/Services/ReceiptService.cs), [QueuedHostedService](../../backend/Background/QueuedHostedService.cs), [BackgroundJobs](../../backend/Background/BackgroundJobs.cs), and [ExpenseDetailPage](../../frontend/src/pages/ExpenseDetailPage.jsx) as the source map. This chapter gives three distinct incident investigations using synthetic evidence. The incidents are teaching scenarios, not reports of events observed on the user's machine.

## Incident one: the command failed but the reason changed

A learner receives a report that a manager attempted to reject an Approved expense, saw a business-rule error, and later found a different rejection reason. The first hypothesis might be stale UI data, but the service ordering provides a more direct candidate: reason assignment precedes transition validation.

Begin with a timeline containing request arrival, actor resolution, visibility check, manager relationship check, reason assignment, transition rejection, error mapping, and later read. Ask which observations distinguish this hypothesis from a separate successful rejection or another concurrent writer.

The service lab supplies a bounded reproduction with current copied code and no concurrent writer. Its assertions establish the changed reason and unchanged lifecycle fields under that fixture. A live incident would still need correlation with the actual request, actor, and data version. Do not replace that correlation with the claim that a known source pattern must explain every similar symptom.

A proposed diagnostic event can record command type, expense identifier, actor category, starting status, rejection category, and whether mutation application began. Avoid logging free-form rejection text unnecessarily; it may contain sensitive business information. A case identifier can connect the diagnostic record to a controlled reproduction without exposing the content.

## Incident two: a receipt remains pending

A pending receipt can reflect several different states: the queue has not accepted work, accepted work has not been dequeued, the worker is delayed, processing failed, metadata was replaced, or the browser stopped polling after a failed refresh. These explanations require different evidence and recovery actions.

Build an observation ladder. First confirm which receipt identifier is current on the expense. Then determine whether its upload request completed, whether enqueue acceptance was observed, whether a job began, whether it found the metadata, and whether it marked the receipt processed. Finally inspect whether the client fetched and displayed the newer state.

The current upload path awaits enqueue acceptance, while the worker wrapper refreshes policy after processing. The current detail page schedules another poll based on state changes and can stop rescheduling after a failed poll under the source-derived sequence discussed earlier. These facts narrow hypotheses but do not establish the actual cause without the relevant runtime observations.

A recovery action should match the evidence. Retrying a browser read is appropriate for stale display. Retrying processing may be appropriate for a known unprocessed current receipt. Reuploading can replace metadata and create new files, so it is a broader action and should not be the first universal suggestion.

## Incident three: reimbursement never reaches completion

An expense in Reimbursing with no payment reference may have work waiting for acceptance, queued but not running, canceled during delay, failed in the worker, or skipped because the expense state changed before the completion check. The current worker's logging and return paths provide limited categories, but the selected system has no durable work ledger.

Separate acceptance evidence from completion evidence. A history note saying reimbursement queued is created during the service transition before observed queue completion in the selected method. It should not be treated as definitive proof that durable work exists. The queue and service labs demonstrate why that inference is too strong.

The simulated job generates a local reference after its delay and state check. There is no external payment provider in this path. An incident report should therefore say simulated reimbursement completion rather than imply money moved. Precise wording prevents operational confusion and keeps the learning exercise grounded.

## Construct an evidence ledger

For each hypothesis, record supporting source, observed runtime facts, missing observations, a smallest next experiment, and a safe recovery proposal. Mark facts with their boundary: client state, HTTP response, service object, queue acceptance, worker execution, or storage state.

A useful ledger might say that the API returned an expense in Reimbursing, but no queue-acceptance observation was captured. Another row might say that the worker logged a missing expense, but the client still displays an older snapshot. These statements can coexist without forcing one vague success or failure label.

Do not fill missing observations with assumptions because a diagram looks cleaner. An unknown state is a legitimate investigation result. The next experiment should reduce that uncertainty without changing unrelated data or restarting components indiscriminately.

## Metrics should follow the lifecycle

Proposed metrics include accepted commands, rejected commands by category, pending queue writers, queued work count, job starts, job completions, job failures, skipped jobs, and age of unresolved work. Each metric needs an exact increment point. A count named reimbursement failures is ambiguous if it combines policy denial, queue rejection, and worker exception.

Latency should also have named endpoints: request duration, time to queue acceptance, queue wait, job duration, and time to client-observed completion. Adding them blindly can double-count overlapping intervals. A trace diagram with timestamps makes the intended measurement clear.

The current application does not expose all these metrics in the inspected files. They are an observability proposal. A test can verify increment points with controlled collaborators before a dashboard is built, preventing attractive charts from measuring the wrong event.

## Correlation is not the same as causation

A request identifier can connect logs from a command, but background work may run after the request scope ends. A proposed work identifier should travel with durable or queued data and appear in worker events. The current delegate-based queue does not automatically provide a durable correlation record.

If two uploads occur close together, correlating only by expense ID can mix R1 and R2. Include receipt or work identity where necessary. If two commands operate on the same expense version, record the version or expected state in a proposed concurrency-aware design.

Avoid interpreting temporal proximity as proof that one event caused another. A failed poll and a worker exception occurring near each other may be independent. Use identifiers and controlled reproductions to establish the link.

## Write a concise incident conclusion

A strong conclusion states the observed symptom, the established boundary failure, the evidence, the limitation, and the next change. For example, the copied service lab proves that invalid rejection can mutate reason before throwing; a proposed fix validates before applying that field and adds an unchanged-state test. That conclusion is narrower and more actionable than saying validation is broken everywhere.

For an unresolved pending receipt, a truthful conclusion may be that current instrumentation cannot distinguish queue delay from processing failure, so the next change is an observable work outcome plus a controlled failure test. Do not fabricate a root cause merely to close the report.

The final report should include synthetic reproduction steps and avoid credential or receipt contents. A reviewer should be able to reproduce the mechanism without access to private business data.

## Independent exercises

Exercise NR22-A: write an evidence ledger for the changed-reason incident and distinguish the executed service characterization from a live-request conclusion.

Exercise NR22-B: build a decision tree for a pending receipt with current R2 and an older queued R1 job. Include browser polling failure and choose the smallest recovery action for each established cause.

Exercise NR22-C: define five lifecycle metrics with exact increment points and a correlation scheme for reimbursement work. Write an honest conclusion for a case where queue acceptance was never observed.
