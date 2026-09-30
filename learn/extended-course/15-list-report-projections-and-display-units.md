# 15. List and report projections, display units, and stale errors

Read [ExpensesPage](../../frontend/src/pages/ExpensesPage.jsx), [ReportsPage](../../frontend/src/pages/ReportsPage.jsx), [format helpers](../../frontend/src/util/format.js), and [ReportingService](../../backend/Services/ReportingService.cs). This chapter connects server data semantics with the exact UI presentation. It asks whether labels, units, loading states, and error states preserve what the server actually returned.

## The list effect has an active-response guard

ExpensesPage tracks status and category filters. Its effect sets loading true, constructs query parameters for nonempty filters, starts a GET, and applies data, errors, and loading completion only while active remains true. Cleanup marks the old effect inactive when filters change or the component unmounts.

This protects against an older filter response overwriting state after its effect has been replaced. It does not abort the underlying request, so old work can still consume resources. The distinction matters when comparing this page with the form and detail pages, whose selected load paths do not use the same guard.

A controlled test can start filter A, switch to filter B, resolve B, then resolve A. The expected list remains B's data because A's callback sees active false. This is a source-derived prediction until a component test runs. The fetch-wrapper lab does not execute React effects.

## Error state is not cleared by a successful filter change

The selected list effect sets loading true at its start but does not set error null. On successful data retrieval it sets expenses, and on failure it sets error. A previous error can therefore remain displayed after a later successful filter request unless another path clears it.

Build a two-step fixture: first fail a request, then change a filter and succeed. Assert both the new rows and the old error visibility. This is a different state bug from stale response overwrite. The active guard can be correct while error lifecycle remains incomplete.

A proposed repair should define whether errors belong to a specific query attempt, the current filter context, or the page as a whole. Clearing at the start of a new attempt and replacing with a new error is one simple policy. Retaining a nonblocking stale-data notice is another, but it should be labeled so users understand which data is trustworthy.

## Loading hides rows but does not erase stored data

The component renders a loading message while loading is true, then an empty-state message if the stored array is empty, otherwise a table. It retains the previous expenses array while a new request is pending. That retained state may become visible again after a failed request when loading becomes false.

A test should distinguish memory retention from visible stale data. During loading the table is hidden by the selected conditional rendering. After failure, the old rows may be shown alongside the error. Whether that is desirable depends on whether the UI labels them as results for the old filter or the current one.

A stronger query-state model can store the query identity with the data. Then the UI can decide whether to show previous results, a retry action, or an empty state without implying the old rows match the new filter. This is a proposed design, not a current feature inferred from the active flag.

## Server filters and client controls have different scope

The list page exposes status and category. ExpenseService.List also accepts date filters through its broader filter record and controller query parameters. The absence of date controls on this page does not mean the server cannot filter by date; it means this selected UI does not send those parameters.

A course exercise can add a proposed date-range control, but its contract must define inclusive endpoints, empty values, invalid ranges, and timezone interpretation. The service compares DateOnly values inclusively. A reversed range naturally selects no matching rows under both comparisons, but a user-facing validation rule might prefer an explicit error.

Do not silently add server pagination to the explanation. The selected service materializes the complete filtered list, and the page renders the returned rows. A scalability proposal should name pagination as new behavior with ordering and cursor or offset semantics, not describe it as already present.

## Currency formatting can hide missing units

The money helper defaults currency to USD and amount to zero when nullish. Expense rows pass each expense's currency explicitly, so their display can retain the row's unit. ReportsPage calls money for aggregate totals without supplying a currency, so the default USD label is used.

ReportingService sums Amount without grouping or converting Currency. Combining that server behavior with the default formatter can display a mixed-currency arithmetic total as USD. This is a stronger cross-layer observation than either source fact alone. It remains a source-derived scenario until a mixed-currency fixture is executed through the UI.

A repair proposal should address the data contract before changing the label. Removing the dollar symbol does not make adding unlike currencies meaningful. Per-currency totals, a restricted single-currency domain, or a documented conversion model require coordinated server and client changes.

## Nullish amounts are not necessarily zero-valued facts

The formatter uses amount ?? 0, so null or undefined becomes a formatted zero. This can make missing data look like a real zero amount. In the current typed server responses, expected fields should be present, but a malformed response or evolving API can expose the fallback.

A display contract should decide where missing values are legitimate. A report with no expenses may correctly return numeric zero. A response missing totalApproved is a contract violation, not necessarily a zero total. A runtime response validator or explicit unavailable display can preserve that distinction.

A bounded formatting test can pass zero, null, undefined, a negative value, and a valid amount with a currency. It establishes helper output under a specified locale/runtime, not correctness of the business amount. Avoid using formatter tests as a substitute for report arithmetic tests.

## Date-only values are displayed through Date

The date helper constructs a JavaScript Date from the supplied value and calls toLocaleDateString without an explicit locale or timezone. ExpenseDate is a date-only business value. Converting it through an instant-like Date representation can make timezone interpretation relevant to the displayed calendar day.

A controlled test should set or simulate the intended timezone and compare a date-only string with the desired displayed date. The course does not claim every environment shifts the day; it identifies the representation boundary that requires evidence. A proposed date-only formatter can avoid treating a calendar date as an instant if that matches the product contract.

The dateTime helper intentionally displays timestamps as local date and time. It should not automatically share the same formatting strategy as DateOnly. Separate helpers with distinct contracts are often clearer than one generic date function that silently handles both categories.

## Reports are loaded once in the selected component

ReportsPage fetches summary in an effect with an empty dependency array. It stores either summary or error and has no selected polling or manual refresh control. A reimbursement completing elsewhere does not automatically update this component through the shown code.

A proposed report-refresh design can use navigation reload, a manual refresh action, polling, or shared invalidation after commands. Choose according to freshness requirements and cost. The detail page's polling does not by itself refresh the report page's separate state.

A test should establish the current one-load behavior before evaluating a new freshness contract. For example, render the report, complete a synthetic reimbursement in another controlled component, and observe whether the report requests new data. Keep cross-component cache assumptions explicit; this code uses direct state rather than a shared query cache in the selected files.

## Independent exercises

Exercise NR15-A: design the out-of-order filter fixture and the error-then-success fixture. Explain why the active flag solves one problem but not the other.

Exercise NR15-B: trace a mixed-currency report from domain rows to aggregate numbers to default USD formatting. Propose a coordinated data-and-display contract and distinguish missing from true zero values.

Exercise NR15-C: design date-only timezone and report-freshness experiments. State which claims belong to pure formatting tests, component tests, and server arithmetic tests.
