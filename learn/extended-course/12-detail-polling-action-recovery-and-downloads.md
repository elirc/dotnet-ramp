# 12. Detail polling, action recovery, and downloads

Read [ExpenseDetailPage](../../frontend/src/pages/ExpenseDetailPage.jsx), [client.js](../../frontend/src/api/client.js), and [receipt download controller](../../backend/Controllers/ReceiptsController.cs). The detail page coordinates background progress, user commands, and a separate binary download path. This chapter traces each asynchronous boundary rather than treating the page as a single load-and-render operation.

## Polling is driven by observed state

The page's load callback fetches the current expense by id and sets expense state, or sets an error message if the request fails. An effect invokes load when the callback changes. Another effect checks whether the receipt is Pending or the expense is Reimbursing. When either condition holds, it schedules load after fifteen hundred milliseconds and clears that timer when the effect cleans up.

This is a recursive timeout pattern driven by state updates, not a permanently running interval. A successful fetch produces a new expense value and causes the effect to evaluate whether another poll is needed. Once neither pending condition holds, no new timeout is scheduled by that effect.

The cleanup prevents a scheduled timeout from firing after that particular effect is replaced or unmounted. It does not cancel a fetch that has already started. Keep timer cancellation and request cancellation as separate behaviors in a test. The historical presence of any timer elsewhere is irrelevant; this chapter follows the actual selected effect.

## A failed poll can stop the cycle

When load fails, it sets error but does not replace expense. If the existing expense reference and load callback remain unchanged, the polling effect's dependencies do not change merely because error changed. The timeout that fired has already completed, so the selected sequence can leave no newly scheduled poll after that failed request.

This is a source-derived recovery scenario to verify with controlled timers and deferred fetch responses. Begin with a Pending receipt, allow one scheduled poll, reject that fetch, then advance time again. The expected current behavior should be characterized before proposing a retry design. Do not claim an observed production polling outage from source inspection alone.

A proposed repair can schedule the next attempt in a controlled loop or explicitly track retry state. It should include a maximum delay, cancellation on unmount or id change, and a rule for permanent errors. Blindly polling forever after an authorization failure is not the same as retrying a transient network error.

## Action success and refresh success are different

The act helper clears errors, sets busy, awaits the supplied command, then awaits load, catches errors, and finally clears busy. But load catches its own fetch error and sets page error without rethrowing. Therefore, a command can succeed, the refresh can fail, and act still reaches its finally through a fulfilled load call.

The UI may retain the older expense data while showing the refresh error. A user who sees an old status might assume the command failed and try again. The page needs a recovery contract that distinguishes command rejection, command accepted with refresh failure, and unknown command outcome due to network or response parsing failure.

The current helper does not store those categories separately. That is a concrete design opportunity, not proof that every user is confused. A test can drive all three paths and inspect displayed status, message, busy state, and enabled actions. The proposed UI should make safe next steps clear without claiming the server rolled back when it did not.

## Busy does not cover every interaction equally

The main action buttons use disabled={busy}. The upload input and download button shown in the selected component do not use the same disabled condition. The edit link also remains a link rather than a disabled command button. This means busy is a partial interaction policy, not a universal page lock.

A useful interaction matrix lists submit, approve, reject, reimburse, delete, edit navigation, upload selection, and download. For each, record whether busy suppresses it and whether concurrent execution would be harmless, confusing, or a separate server concern. The correct answer is not necessarily to disable everything; it is to define which combinations the product supports.

For example, downloading an existing receipt during a status refresh may be acceptable, while starting another upload during a pending upload could create replacement races. A proposed client restriction should complement server invariants rather than substitute for them.

## Deletion has a post-navigation refresh

The delete action passed to act awaits the DELETE request and navigates to the list page. After the supplied function resolves, act still calls load. Depending on component lifecycle and timing, that callback can attempt to reload the deleted expense or update state after navigation. The source establishes the sequence; a React integration test should characterize the actual mounted behavior.

A smaller action abstraction could distinguish commands that remain on the detail page from commands that navigate away. Another proposal could let each action return a refresh decision. The acceptance criterion is not merely fewer network calls; it is a clear successful deletion experience without a misleading not-found error from an unnecessary follow-up read.

A controlled test should assert navigation, command count, and whether a detail GET occurs afterward. It should not depend on arbitrary delays or a real backend. Deferred promises and a route harness can expose the order reproducibly.

## The download path bypasses the wrapper

downloadReceipt calls fetch directly, adds a bearer header from tokenStore, converts the response to a blob, creates an object URL, clicks a temporary anchor, and revokes the URL. It does not check response.ok before treating the body as a file, and the shown promise chain has no catch that reports an error to page state.

A failing JSON response can therefore be converted into a blob by this code path rather than becoming ApiError through client.js. A proposed download test should return a synthetic 403 ProblemDetails response and verify that no success download is triggered under the desired repair contract. The actual selected behavior remains a source prediction until a controlled DOM test executes it.

The binary endpoint itself authorizes through receipt and expense visibility before returning a stream. That server-side contract does not make every client response a valid receipt. Network intermediaries, expired sessions, and missing files can still produce errors that the client must distinguish from binary success.

## Object URL lifetime is another resource boundary

The selected code revokes the object URL immediately after invoking the anchor click. A browser-specific test would be needed to establish whether this timing is reliable across the intended environments. The course does not infer a universal browser guarantee from the presence of revokeObjectURL.

A robust proposal should define cleanup on success and failure, avoid leaking URLs, and provide an accessible error path. The temporary anchor's behavior and filename should be tested with synthetic data. Do not download real receipts merely to verify resource cleanup in an educational exercise.

## Route identity and late responses

load is recreated when id changes, but an earlier in-flight load can still resolve and call setExpense because the selected callback does not check request identity. A controlled A-then-B navigation test can expose whether a late A response overwrites B. This is related to the form race but has different consequences: available actions and receipt identifiers can now correspond to the wrong displayed route.

A proposed repair should bind responses to the active id and coordinate polling cleanup with request cancellation or generation checks. It must also preserve ordinary reload after a successful action. A blanket ignore-after-first-load rule would prevent legitimate background progress updates.

## Independent exercises

Exercise NR12-A: draw the timeout, fetch, state update, and effect-cleanup sequence for a successful poll and a failed poll. Explain why clearing a timeout does not cancel an already-started request.

Exercise NR12-B: distinguish command rejection, accepted command with refresh failure, and unknown command outcome. Design the visible recovery message and allowed next action for each, including deletion navigation.

Exercise NR12-C: specify a controlled download test for a failing JSON response and an out-of-order detail-load test. Keep DOM, browser, server authorization, and wrapper evidence separate.
