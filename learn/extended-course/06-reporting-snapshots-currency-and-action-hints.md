# 06. Reporting snapshots, currency, and action hints

Read [ReportingService](../../backend/Services/ReportingService.cs), [ExpenseService](../../backend/Services/ExpenseService.cs), [Mappings](../../backend/Services/Mappings.cs), and [DataStore](../../backend/Storage/DataStore.cs). This chapter follows information from mutable entities into summaries and response hints. Its central question is what a displayed number or action actually guarantees.

## Reports reuse visibility, then aggregate

Summarize calls ExpenseService.List with date filters and no status or category filter. That reuses the service's visibility policy before aggregation. An Employee receives their own visible expenses, a Manager their own and direct reports' expenses, and Finance the broader selected set. A report total should therefore be interpreted relative to the actor and filter, not as an unconditional organization-wide number.

The summary computes TotalSubmitted from Submitted, TotalApproved from Approved and Reimbursing, and TotalReimbursed from Reimbursed. PendingApprovalCount counts Submitted records. The category and status breakdowns group the complete visible filtered list, so their scope includes states beyond those named headline totals.

Use a fixture with Draft, Submitted, Approved, Reimbursing, Reimbursed, and Rejected expenses. Give each a distinct amount. Compute the headline fields and grouped fields independently. This exposes a common mistake: assuming TotalApproved means only the Approved status or assuming category totals include only reimbursed spending.

## A worked amount table

Suppose the visible set has amounts ten in Draft, twenty in Submitted, thirty in Approved, forty in Reimbursing, fifty in Reimbursed, and sixty in Rejected. Under those explicit inputs, TotalSubmitted is twenty, TotalApproved is seventy, TotalReimbursed is fifty, and PendingApprovalCount is one. Grouped totals across all statuses sum to two hundred ten if every row falls in the selected date range.

This calculation is a source-derived worked example, not a report captured from the running app. It gives you a reproducible oracle for a future service test. To test visibility, add an unrelated employee's expense with a distinctive amount and verify that it enters or leaves the result according to the actor's scope.

Keep amount and count separate. Two submitted expenses of equal amount produce a count of two even if a mistaken deduplication by amount would collapse them. Likewise, a zero amount can contribute a row count without increasing a total. Policy validity and inclusion in a report are separate questions in the inspected aggregation path.

## Currency is stored but not grouped here

Expense carries Currency, and create or update normalizes its text. ReportingService sums Amount without grouping by currency or converting values. Therefore, a mixed-currency fixture exposes a semantic limitation of the selected summary: numeric amounts are added even when their units differ.

Do not repair this by inventing an exchange rate in a test. First choose a product contract. One proposal groups totals by currency. Another requires a normalized reporting currency with an explicit rate source, effective date, rounding rule, and stored conversion evidence. A third restricts accepted expenses to one currency. These choices have different data and user-interface implications.

For a bounded course exercise, use synthetic USD and EUR rows and state that the current sum is merely the arithmetic implemented by the method. Then write the proposed output shape for per-currency totals. This teaches unit-aware modeling without making financial advice or current exchange-rate claims.

## Concurrent dictionaries do not freeze entities

DataStore uses ConcurrentDictionary for users, expenses, receipts, and sessions. That supports thread-safe individual dictionary operations, but the Expense values are mutable objects containing mutable lists and settable properties. A dictionary operation does not automatically make a multi-field business transition atomic or create a consistent report snapshot.

ExpenseService.List materializes a list of selected object references. Subsequent aggregation reads their properties. If another thread changes those objects while a report is being built, a complete consistency claim would require additional synchronization or immutable snapshots. The current course has not executed a concurrency stress test, so describe this as an architectural evidence gap and a proposed controlled experiment rather than an observed inconsistent production report.

A deterministic experiment can pause a reporting projection between two reads while a controlled collaborator changes a status or amount. The purpose is to test a declared snapshot contract. Without such a contract, a test that merely observes occasional variation may be hard to interpret. Decide whether the report promises one instant, eventual consistency, or a best-effort current view.

## Action hints are another projection

Mappings includes AvailableActions in the expense response. It checks ownership and editable state for edit and upload, adds delete for an owned Draft, adds submit for an owner when the Employee transition is legal, adds approval actions for a direct-report Manager on Submitted, and adds reimbursement for Finance on Approved.

The submit hint checks CanTransition with UserRole.Employee rather than the actor's actual role. Therefore an owner whose actual role is Manager can be offered submit under the mapping while the service's actual role check rejects that transition, assuming earlier policy checks pass. The [service characterization lab](13-service-characterization-and-independent-snapshots.md) executes this specific mismatch through copied current mapping and service code. It does not imply every hint is wrong or establish that a real browser displayed the case; the fixture deliberately isolates ownership, actor role, and valid policy inputs.

The hint also does not evaluate current blocking policy flags before adding submit. A visible submit action can therefore lead to a policy rejection. That may be an intentional user experience if the action explains missing requirements, or it may be a design gap. The review should decide what AvailableActions means: potentially attemptable actions, currently guaranteed actions, or role-and-state hints only.

## Hints must not become authorization

Even a perfectly computed action list can become stale before the next request. The service must continue enforcing its own ownership, relationship, policy, and transition rules. A client hiding a button is not a security boundary, and a client displaying one is not a promise that a later command will succeed.

A useful contract can explicitly call these hints and provide a reason list for disabled or unavailable actions. Another design can return a version with the response and require a matching command version. Neither proposal is present merely because the current response includes an action array. Keep the proposed concurrency and user-experience guarantees separate.

The test matrix should compare mapping hints with service outcomes under carefully controlled fixtures. Where they differ, decide whether the difference is allowed by the chosen contract. Do not make a blanket assertion that every listed action must succeed unless policy and concurrency are deliberately included in that contract.

## Projection safety and omission

The mapping returns user identity fields and display data rather than password hash fields. Receipt responses omit the stored disk filename and return selected metadata. Expense events map actor name but not every internal event field. These are deliberate projection boundaries visible in the selected code.

A regression test can assert the public DTO shape and absence of internal storage fields. However, a type-level mapping test is not a full serialization or HTTP evidence claim. To prove what a client receives, execute the relevant serializer or endpoint with synthetic data and inspect the response. The course currently offers the source trace and proposed test design.

## Independent exercises

Exercise NR06-A: compute the six-status worked example and add one out-of-scope employee. Explain the difference between headline totals, grouped totals, and PendingApprovalCount.

Exercise NR06-B: propose a mixed-currency reporting contract and a controlled snapshot experiment. State why ConcurrentDictionary alone does not establish the chosen business consistency guarantee.

Exercise NR06-C: construct an owned Manager Draft with otherwise valid policy inputs. Compare the submit hint and service transition. Propose a precise meaning for AvailableActions and identify which authorization checks must remain on the command path.
