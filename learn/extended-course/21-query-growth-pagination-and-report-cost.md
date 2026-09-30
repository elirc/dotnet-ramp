# 21. Query growth, pagination, and report cost

Read [ExpenseService.List](../../backend/Services/ExpenseService.cs), [ReportingService](../../backend/Services/ReportingService.cs), and [ExpensesPage](../../frontend/src/pages/ExpensesPage.jsx). This module develops a performance plan from actual operations and output contracts. It does not claim a benchmark was run or that the current demo requires a particular database or caching product.

## Count work before timing it

List begins with all expense values, applies role visibility, optional filters, and descending UpdatedAt ordering, then materializes a list. The browser renders all returned rows. As the dataset grows, scanning, sorting, response size, and DOM rendering can each contribute cost.

A timing number alone does not identify which cost dominates. First record input count, visible count, filtered count, returned count, serialized bytes, and rendered rows. These measurements describe different stages. An employee with few visible expenses can still incur a broad in-memory scan under the selected structure.

Reporting calls List, then performs several passes for headline totals and grouped projections. Multiple passes over a small in-memory list can be perfectly acceptable. A performance proposal should use measured scale requirements rather than treat every repeated enumeration as an urgent defect.

## Define a representative workload

Use synthetic expenses distributed across owners, managers, categories, dates, and statuses. Include a manager with many direct reports, an employee with a small personal set, and Finance with the broadest view. Include equal UpdatedAt values so ordering assumptions are exercised.

Do not benchmark only one actor and extrapolate to every role. Visibility predicates and result sizes differ. Also distinguish cold startup or data seeding from steady-state request work. A benchmark report should say whether those costs are included.

For reproducibility, record dataset size and generation seed or explicit fixture recipe. Use synthetic merchant names and amounts. The purpose is to explore algorithmic growth without exporting real expense information.

## Pagination requires a total order

The current list returns the full filtered result and orders by UpdatedAt descending. A proposed paginated API needs an explicit tie-breaker such as identifier so every row has a deterministic position under a stable dataset. Without that, equal timestamps can move unpredictably between pages.

Offset pagination is simple but can repeat or skip rows when newer expenses are inserted before the current offset. Cursor pagination can continue after a stable ordering tuple, but must define how updates to UpdatedAt affect rows already seen. Because editing an expense changes UpdatedAt, this application has a mutable sort key, which complicates snapshot-like browsing.

A design can accept live-list movement, use a snapshot boundary, or choose a different stable ordering for the browsing session. Each is a product contract. Do not promise no duplicates across pages unless the chosen mechanism actually supports that guarantee under updates.

## Work through a mutable-sort example

Start with E3, E2, and E1 ordered newest first and page size two. The first page contains E3 and E2. Before the next page, E1 is edited and becomes newest. An offset-based second page can now return a row already seen or omit the moved row depending on the exact new ordering.

A cursor based on E2's old tuple can avoid some insertion shifts, but E1's updated tuple now lies before the cursor and may not appear in later pages. That is not necessarily a bug if the contract is live forward traversal; it is a limitation that should be explained to users or handled through refresh.

A test matrix should include insertion, deletion, update of a not-yet-seen row, update of an already-seen row, and equal timestamps. The expected result must follow the declared browsing contract rather than a vague notion of correct pagination.

## Reports need complete scope despite paging

If List becomes paginated, ReportingService cannot simply aggregate the first page and preserve current summary semantics. The report needs a separate complete aggregation query or an explicit paginated-report contract. Reusing a convenient list API without examining its new semantics can silently undercount totals.

This is a common migration hazard: a performance improvement in one endpoint changes a shared service method's meaning. Separate query methods or explicit options can preserve clarity. Tests should retain the six-status worked oracle and add a dataset larger than one page so accidental first-page aggregation fails visibly.

The response should also retain actor scope and date filters. A fast organization-wide total is not a valid replacement for a slower employee-scoped total. Correctness constraints define which optimizations are acceptable.

## Caching has an identity problem

A proposed report cache key must include every input that changes the result: actor or visibility scope, date filters, relevant policy or schema version if applicable, and data freshness semantics. Caching only by URL can be unsafe if authenticated users receive different data from the same route.

Invalidation must account for create, update, delete, transition, receipt-related changes that affect displayed flags, and manager relationship changes if those alter visibility. Not every field affects every report, so a precise dependency map can reduce unnecessary invalidation. But omitting a scope-changing dependency can expose stale or incorrectly scoped results.

The current selected frontend does not use a shared query cache for these pages. A caching design is therefore a proposed extension, not an explanation of present behavior. Begin with a bounded cache model and actor-isolation tests before choosing a library or distributed store.

## Measure UI cost separately

The list page maps each returned expense into a table row with date, money, status, and link formatting. Large result sets can increase rendering and interaction cost even if the server responds quickly. A browser performance experiment should measure render duration and responsiveness with synthetic data, not only API latency.

Virtualization or paging can reduce DOM work, but each changes accessibility and navigation behavior. A proposed design should preserve table semantics or provide an accessible alternative, maintain focus during updates, and make loading and empty states clear. These are acceptance criteria to test, not implementation details to expose to ordinary users.

The money helper constructs an Intl.NumberFormat on each call. A measured formatting hotspot could motivate reuse by currency and locale, but do not optimize it solely because construction appears in source. Profile representative rendering first and retain formatting-correctness tests around any cache.

## Performance evidence should include correctness

A benchmark result should be paired with output checks: same visible identifiers, same totals, same ordering contract, and same error behavior. Faster incorrect results are not an improvement. Use checksums or independent aggregate oracles for large synthetic datasets, then inspect small counterexamples when they differ.

Report medians and variability only if actually measured. The current course offers a plan and source cost model, not invented latency numbers. A useful proposal can say which measurements would decide between simpler paging and a more elaborate query redesign without pretending the decision is already settled.

## Independent exercises

Exercise NR21-A: define a three-role workload and the counts to collect before timing. Explain why server scan count, response size, and browser row count are different measurements.

Exercise NR21-B: trace mutable UpdatedAt pagination with insertion and edit cases. Choose a browsing contract and an independent oracle for equal timestamps and moved rows.

Exercise NR21-C: design a report query and cache identity that preserve scope after list pagination. Include a test that would catch aggregation of only the first page and a test for actor isolation.
