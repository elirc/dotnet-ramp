# 07. Policy boundaries, deterministic time, and reason codes

Read [PolicyEngine](../../backend/Services/PolicyEngine.cs), [Expense](../../backend/Domain/Expense.cs), and the policy-refresh call sites in [ExpenseService](../../backend/Services/ExpenseService.cs). This chapter turns a small collection of if statements into a testable decision specification. It emphasizes boundaries, independent inputs, and the difference between a warning and a blocking result.

## Treat each flag as an observable decision

The engine returns a list of PolicyFlag records containing a code, message, and severity. Those three fields serve different consumers. Code is a stable candidate for assertions or user-interface behavior. Message communicates a human explanation. Severity drives HasBlocking. A test that checks only list length loses information about which rule fired and whether it blocks submission.

Begin with a clean fixture: positive amount below relevant limits, nonempty description, today's date, and receipt conditions chosen to avoid warnings. Then vary one input at a time. This isolates the rule under study and makes a failing prediction explainable. Once single-rule cases are understood, deliberately combine rules to test accumulation.

Do not assume the flags are mutually exclusive. A high amount can exceed its category limit and the high-value threshold while also lacking a required receipt. A missing description can coexist with a future date. The expected result is a set or ordered list of relevant flags according to the chosen contract, not a single most important error unless a later presentation layer chooses one.

## Build a boundary ladder for amount

The amount-invalid rule uses a nonpositive comparison. The missing-receipt rule uses an amount greater than twenty-five. The high-value rule uses an amount greater than one thousand. Category limits use their own greater-than comparisons. Each boundary needs below, equal, and above cases, but not every case needs an expensive full application test.

For the receipt threshold, use twenty-four ninety-nine, twenty-five, and twenty-five oh-one with otherwise identical valid inputs. For invalid amount, use a negative cent, zero, and a positive cent. For high value, use nine hundred ninety-nine ninety-nine, one thousand, and one thousand plus a cent. Keep decimal arithmetic in the fixture rather than introducing binary floating-point approximations into a domain that uses decimal.

A category can create overlapping thresholds. Software's selected soft limit is one thousand, so an amount just above that value can generate both OVER_CATEGORY_LIMIT and HIGH_VALUE. Meals has a much smaller soft limit, so a meal can exceed its category limit without being high value. This comparison is a useful exercise in independently triggered warnings.

## Receipt identity and receipt state are separate inputs

The policy receives an Expense and a boolean indicating whether its receipt is processed. The missing-receipt branch examines ReceiptId, while the processing warning combines a nonnull ReceiptId with the supplied processed state. RefreshPolicy computes that boolean by looking up the current receipt in the store and checking its status.

A dangling ReceiptId therefore deserves its own fixture. The expense says a receipt is attached, but the lookup may fail, making hasProcessedReceipt false. Under the selected evaluation branches, that resembles an attached but unprocessed receipt rather than an absent identifier. A robust product design may want to distinguish missing metadata from pending processing, but that would be a new rule or flag.

Do not make the boolean more authoritative than its derivation. In a pure PolicyEngine test you can supply true directly, even for an inconsistent expense. A service-level test should establish that RefreshPolicy derives it from the store as intended. Both tests are useful, but they answer different questions.

## Dates need a stable reference point

The engine derives today from DateTime.UtcNow and compares DateOnly values. A future date blocks. A date older than ninety days warns, while exactly ninety days old does not satisfy the strict older-than comparison. The future and stale checks form an if-and-else-if sequence, so a date cannot take both branches in one evaluation.

A test that hard-codes a calendar date as today eventually becomes stale. A test that computes today immediately before calling the engine can still cross a UTC midnight boundary between the two reads. That risk is small but conceptually important when teaching deterministic tests. A proposed clock seam would make the reference date an explicit input and remove that uncertainty.

There are several possible seams. The engine could accept a DateOnly today parameter, receive a clock abstraction, or separate a pure evaluation core from a wrapper that reads the clock. Choose a design according to the application boundary. Do not add a large clock framework merely to test one date calculation unless the broader project benefits from it.

## Distinguish characterization from a changed requirement

A characterization test should reflect current strict thresholds and severities. A product requirement may later say that an unprocessed receipt should block, that stale means ninety days or older, or that a category limit is a hard cap. Those are policy changes, not corrections to an oracle that misunderstood the current source.

Write the old and proposed truth tables next to each other. Identify exactly which rows change and what the user would experience. For example, changing the stale comparison from older-than to at-least changes only the equality boundary, but changing a warning to blocking changes submission eligibility for every matching expense.

This discipline prevents a test suite from silently becoming a design document that disagrees with the product. A reviewer should be able to see whether a patch preserves behavior, fixes an implementation against an existing requirement, or intentionally changes the requirement.

## Refresh timing creates a consistency question

Create and Update refresh policy after assigning fields. Submit refreshes before checking for blocking flags and before its lifecycle transition. Receipt upload refreshes after attaching a receipt, and its queued wrapper refreshes after processing. These are distinct moments at which the list can change.

A response can therefore contain policy flags computed at a particular moment, while later receipt processing changes the inputs. A client that caches the response needs a refresh strategy if it wants current flags. The selected source trace does not prove that every user-interface view refreshes at the right time; that requires reading and testing the relevant client flow.

Because PolicyFlags is a mutable settable list, replacing it on a failed submission is still an object mutation. If you adopt an all-fields-unchanged rejection contract, decide whether policy refresh is an allowed observational update or must also be deferred. This is a product and domain choice, not something an exception type decides automatically.

## Design a layered test suite

Pure policy tests should cover boundary values, exact codes, and severities with a controlled date seam if implemented. RefreshPolicy tests should cover absent, pending, processed, failed, and dangling receipt references. Submission tests should cover warning-only acceptance, blocking rejection, and invalid lifecycle state after a refresh.

A small number of HTTP tests can then establish how flags and failures appear to a client. Avoid repeating every decimal boundary through the host if the pure layer already establishes the rule. Use the higher layer for binding, authentication, serialization, and response contracts that lower layers cannot prove.

Mutation proposals should target meaningful changes: greater-than to greater-than-or-equal, warning to blocking, or use of receipt existence instead of processed status. A useful mutation compiles and changes an observable decision. The current lifecycle runner does not execute these policy mutations; they are a plan for a future bounded policy lab.

## Independent exercises

Exercise NR07-A: construct amount ladders for invalid amount, required receipt, category limit, and high value. Include one case with multiple flags and specify codes and severities rather than only the count.

Exercise NR07-B: compare absent ReceiptId, dangling ReceiptId, pending receipt, and processed receipt. Separate a pure engine input from the RefreshPolicy derivation and predict submission eligibility under otherwise valid inputs.

Exercise NR07-C: design a deterministic-date seam and a three-layer test plan. Include a proposed policy change at an equality boundary and explain how its acceptance criteria differ from current characterization.
