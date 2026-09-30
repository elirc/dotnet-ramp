# Client and service solutions: chapters 10-15

Use these answers after making independent predictions. Each exercise is worth ten points: four for the current behavior, three for a concrete fixture, and three for evidence limits or proposed-contract reasoning. The eighteen exercises in this guide total one hundred eighty points. A correct proposed repair must preserve ordinary behavior and state which current characterization it intentionally changes.

## NR10-A: response matrix

A 204 returns null without reading text. A successful JSON object is parsed and returned. A successful empty body returns null. A failing JSON ProblemDetails response becomes ApiError, using detail before title before message. A failing message-only response uses message. A failing empty response uses the status fallback. A failing non-JSON body throws during JSON.parse, and a rejected fetch propagates its original error.

The last two cases do not receive the wrapper's normal status-and-details fields. Parsing a body before checking res.ok is the causal reason, so moving only the message-selection expression would not fix the non-JSON case. A robust fixture must use the same failing status with different body representations to isolate that distinction.

The server may already have performed a mutation before any response interpretation fails. Your matrix should therefore avoid a column that equates client rejection with server rollback. Record response interpretation and business outcome as separate dimensions.

## NR10-B: assertion map

The twelve assertions cover successful JSON parsing; absent-token headers; token plus JSON headers; serialized body; form forwarding; omitted form Content-Type; 204 short-circuit; ProblemDetails precedence and details; empty-error fallback; non-JSON SyntaxError; original network error propagation; and token clearing. The source hash comparison is a preservation check in addition to those behavioral assertions.

The runner does not mount AuthProvider, invoke the detail download chain, or execute browser FormData encoding. It also does not test storage failures or abort behavior. A smallest next wrapper test could make localStorage.getItem throw and assert whether fetch is reached, but a component race needs a React harness rather than another fake response alone.

Credit requires naming the actual boundary. Saying the frontend was tested is too broad. Saying client.js was imported under Node with controlled fetch and localStorage accurately describes the executed evidence and still gives a maintainer useful confidence in those branches.

## NR10-C: unreadable successful mutation response

A defensible proposal reports that the request outcome could not be confirmed and offers reconciliation before retry. If the API has a command identity, query that identity. If it only has the expense identifier, reload authoritative expense state and compare the intended transition where possible. Creation without an identifier or idempotency record remains harder to reconcile.

The wrapper can normalize a parse failure into a structured transport-contract error, preserving status and a category such as unreadable response. It should not claim the business command was rejected or silently return an empty object. The caller needs enough information to choose a safe next step.

A test should make the synthetic server-side effect occur before returning malformed success text, then assert that the UI does not automatically submit a second command. This would be a new controlled integration experiment; the existing client lab establishes only the parsing behavior.

## NR11-A: amount and validation layers

The browser stores typed input text, submit applies parseFloat, JSON serialization emits the resulting number representation, MVC binds the request into decimal, and the service stores that decimal. A loaded edit value can begin as a number rather than input text, so fixtures should include both ordinary entry and loaded editing.

The form has required, minimum, and cent-step constraints but does not show the DTO's one-million maximum. Direct HTTP clients bypass browser controls. Direct service calls also bypass automatic MVC attribute validation unless the test invokes it separately. Domain policy and lifecycle checks still have their own selected call paths.

A good fixture records each representation and expected rejecting layer. It does not use a malformed value and then attribute the failure to a later guard that was never reached. For currency, the current service uppercases without trimming; the source-bound service lab characterizes this only through a direct call that may not satisfy the HTTP length rule.

## NR11-B: date freshness and edit races

To test the default date, import the module under a controlled time, advance across the intended date boundary, and mount a new create form. The current EMPTY value was constructed at module initialization, so the initial date can reflect the earlier time. A fresh state initializer would address that lifetime issue but still needs a decision about UTC versus business-local date.

For the edit race, start A's request, change id to B, resolve B, and then resolve A. The desired repaired contract keeps B's fields because B owns the current route context. A request-generation check or cancellation with ignored late completion can enforce that contract.

Do not use an arbitrary sleep to simulate the race. Deferred promises make the order explicit. Also assert the normal single-request case, because a repair that suppresses every completion can pass a stale-response test while leaving the form unusable.

## NR11-C: draft save and submit

Create can save a structurally valid Draft while RefreshPolicy records a blocking receipt requirement. Submit later reevaluates policy and rejects while that blocking flag remains. The workflow should let the user see the saved draft, attach an acceptable receipt under the chosen policy, and retry submission after current state is refreshed.

An error contract should distinguish browser prevention, server model validation, domain policy rejection, and stale or invalid lifecycle state. Field-level messages belong to structural input problems; policy messages explain business requirements; stale-state messages should prompt reconciliation rather than editing unrelated fields.

The current wrapper consumes a details array but does not flatten every framework validation-error shape. A proposed normalizer should define stable field identifiers and preserve general errors separately. A successful draft save is evidence of persistence at that boundary, not proof that the later submit command will succeed.

## NR12-A: polling timeline

A pending expense snapshot causes the effect to schedule one timeout. When it fires, load begins a request. A successful response sets a new expense object, causing the effect to reevaluate and schedule another timeout if pending remains true. Cleanup clears the timeout associated with the previous effect.

On a failed request, load changes error but leaves expense unchanged. Under the selected dependencies, that alone does not schedule the next poll, so the cycle can stop after the fired timeout. A controlled timer test should verify this exact sequence before evaluating a repair.

Clearing a timeout only prevents a callback that has not yet fired. It does not abort a fetch already started by that callback. A complete proposal needs both scheduling ownership and late-response ownership, especially when the route id changes during a request.

## NR12-B: action outcomes

Command rejection means the server explicitly denied the command under the observed response contract; show the reason and preserve or refresh the appropriate state. Accepted command with refresh failure means the mutation succeeded but the displayed snapshot could be stale; show a refresh-specific message and avoid implying rollback. Unknown outcome means the client cannot establish whether the command was accepted and should reconcile before repeating it.

The selected act helper cannot fully represent these categories with one error string, and load catches its own refresh error. A richer proposed state machine should preserve command outcome separately from refresh outcome.

Deletion adds navigation: the supplied function navigates after DELETE, then act still invokes load. A repaired contract can skip detail refresh for navigation-away actions. A test should assert command count, navigation, and absence of an unnecessary deleted-resource read rather than merely checking that the delete button became unbusy.

## NR12-C: download and route identity

Return a synthetic failing JSON response from the direct download fetch. Under the desired repair contract, response status is checked before blob download, an accessible error is shown, and no anchor click occurs. The current source lacks that check and catch path, but the course has not executed a DOM test of it.

For route identity, start a detail request for A, navigate to B, resolve B, then resolve A. A repaired implementation should retain B's expense, actions, and receipt identity. Test those fields together because stale actions can be more harmful than a stale heading.

The client wrapper lab does not cover this direct fetch chain or React state. The receipt controller's authorization checks do not guarantee every response body is a valid file. Keep server permission, transport response, DOM download, and component lifecycle evidence distinct.

## NR13-A: guard reachability

A Manager-owned Submitted expense passes visibility through ownership but fails the direct-report relationship when the manager is not their own report. Removing only that relationship condition lets approval proceed, so the mutation becomes observable. An unrelated manager may fail GetForActor first and therefore cannot isolate the later guard.

To isolate ownership from role, use two Employee actors with the same role and an expense owned by only one. A direct Update or Submit call by the other should fail GetOwned before policy or lifecycle work. Keep the expense otherwise editable and valid so no unrelated condition obscures the boundary.

A good answer names both the guard under test and the earlier guards deliberately satisfied. This reasoning is more informative than reporting broad authorization coverage without showing why the fixture reaches the intended line.

## NR13-B: assertion categories

The rejection reason, policy-list replacement, and currency-space checks characterize current behavior, including behavior a repair may intentionally change. The manager-own approval check protects an authorization relationship. The mapped submit hint followed by role rejection establishes a projection-versus-command mismatch. The pending queue gate establishes handoff ordering.

Pending-receipt submission and its history edge characterize policy severity and successful lifecycle behavior. They should remain valid unless the product deliberately changes pending receipts from warning to blocking. The baseline's exact assertion count is twenty-two, but counting alone does not express these distinct purposes.

Under a rejection-atomicity repair, the expected reason-after-failure assertion should change to the old reason, while status, timestamp, and history preservation remain. Document that contract change explicitly rather than treating a repaired behavior as an unexplained regression in the educational runner.

## NR13-C: snapshots and stub scope

Copy scalar values, policy records, and history records into an independent comparison representation before the command. A reference to the same mutable list is not a historical snapshot. Decide whether equal contents with a replaced list are acceptable, and assert according to that decision.

The job-factory stub is acceptable because the service lab only needs a signature-compatible delegate to reach EnqueueAsync. It never invokes that delegate. The controlled queue reveals whether the service observes acceptance, while the separate actual queue lab establishes capacity behavior.

The stub cannot prove real reimbursement delay, cancellation, payment-reference generation, worker lookup, system transition, or completion history. A worker test would need the real job source and its dependencies. A host test would add dependency-injection scope and loop behavior beyond that.

## NR14-A: bootstrap races

Successful active bootstrap sets user and later loading false. After cleanup, the active checks suppress those state assignments, but the catch still clears token storage. An old successful bootstrap can also compete with a newer login if the effect remains active, while an old failed bootstrap can clear a newer stored token.

A fixture must establish the provider lifecycle and overlap rather than assume the UI makes every sequence reachable. Use deferred current-user and login promises and record storage writes separately from state writes. The key insight is that active protects only the guarded operations.

A proposed generation rule can make each bootstrap result conditional on the session generation that initiated it. The test should verify both stale failure and stale success, because guarding only the catch still leaves old user data able to replace a newer session.

## NR14-B: session generation

Assign a nonsecret generation number whenever login or logout changes local session ownership. Each request that can affect auth state records the generation at start. A late authentication failure may clear state only if that generation is still current under the proposed contract.

Test token A's request, successful login to B, then A's late rejection. B remains authenticated and its token remains stored. Also test a current-generation rejection, which should trigger the chosen expiry recovery. This pair prevents a repair that simply ignores every 401 from appearing correct.

Logs should use request and generation identifiers, not bearer token values. The exact choice to retain or clear a token on transient validation failure is a product policy; the generation mechanism ensures that whichever policy is chosen applies to the right session.

## NR14-C: logout and route protection

Current logout awaits a server attempt, ignores a rejection, then clears local token and user. That establishes local sign-out after a settled attempt, not confirmed revocation when the request failed. A never-settling request can delay local clearing because the wrapper has no explicit timeout in this path.

A bounded proposal can clear local state promptly while treating server revocation as a separately observed best-effort action or a managed retry, according to requirements. It should not tell the user every remote session was revoked without evidence.

RequireAuth tests cover loading display, redirect, and rendering children. They do not prove server authorization or role permissions. Pair them with host/service tests rather than treating a hidden route or button as the security boundary.

## NR15-A: list state

The active flag prevents a replaced filter effect's late response from applying expenses, error, or loading state. A B response remains authoritative after A resolves late. But the effect never clears a previous error at a new request or on success, so an error-then-success sequence can show fresh rows with stale error text.

Use separate fixtures because the mechanisms differ. The first controls response order; the second controls failure followed by a successful new filter. A proposed query-state object can associate data and error with a query identity, avoiding ambiguity about whether retained rows match the current controls.

The current component retains old expenses while loading but hides the table during loading. After failure, those rows can reappear with the error. A correct answer distinguishes stored data from visible data and states the intended stale-data presentation contract.

## NR15-B: units and missing values

The server aggregates decimal amounts without currency grouping, while report calls to money omit a currency and therefore use USD. A mixed-currency fixture can consequently produce an arithmetic sum displayed with a misleading single unit. Repair the aggregate contract and display together, for example by returning separate totals per currency.

The formatter's nullish fallback turns missing amounts into zero. A true empty-cohort zero and a missing required response field are different facts. A response validator or explicit unavailable display can preserve that distinction, while normal numeric zero remains formatted normally.

Tests should include a mixed-unit dataset, an empty valid report, and a malformed response missing a total. One formatting assertion cannot establish business arithmetic or API completeness. Each layer needs the smallest appropriate oracle.

## NR15-C: date and freshness experiments

A date-only test should define the intended timezone and expected calendar day, then compare the helper's Date conversion and localized output. A timestamp-display test has a different contract because local instant rendering may be intentional. Do not reuse one expectation for both merely because both values contain date text.

ReportsPage loads once in its selected effect and has no shown refresh trigger. A component test can establish whether a later reimbursement completion causes another request; a proposed freshness design can then add a deliberate trigger and test it.

Pure formatting tests establish helper output, component tests establish load and display behavior, and server tests establish totals and scope. A complete evidence packet combines these boundaries without claiming a single test covers all three.
