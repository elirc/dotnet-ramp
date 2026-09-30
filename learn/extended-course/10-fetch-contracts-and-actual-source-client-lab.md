# 10. Fetch contracts and an actual-source client lab

Read [client.js](../../frontend/src/api/client.js), then run the [client lab](labs/client_lab.mjs). Unlike the Perch educational model, this lab imports the actual selected JavaScript module. It replaces fetch and localStorage with controlled in-memory collaborators, so it establishes wrapper behavior without a browser, server, real token, or network request.

```powershell
node C:/Users/Owner/Desktop/.netramp/learn/extended-course/labs/client_lab.mjs
```

The runner requires Node and uses only built-in modules. It reads the source bytes, imports them through a data URL, executes twelve assertions, restores the globals, and compares the source hash afterward. A successful result does not establish browser storage policy, HTTP middleware behavior, React rendering, or actual multipart encoding. Those are intentionally outside this boundary.

## The wrapper has an ordered protocol

request begins by obtaining the current token from tokenStore. If a token exists, it adds an Authorization header. It then decides whether the supplied body is form data or JSON. A form body is forwarded directly and the wrapper does not set Content-Type; a defined non-form body is JSON-stringified and receives application/json.

This order matters when a session changes between calls. The token is read for each request rather than captured once when the module loads. A controlled test can set one synthetic token, make a request, replace it, and make another request. The observed header follows the value read at invocation. That does not mean a request already in flight changes its authorization header when storage changes later.

The lab checks the absent-token case, the stored-token JSON case, and direct form forwarding. Its form object is a stand-in, not a real browser FormData instance. The assertion establishes that the wrapper preserves the object and omits a forced Content-Type. It does not prove the browser creates a valid multipart boundary; an actual browser or compatible runtime integration test would establish that next layer.

## Response parsing happens before error classification

After fetch resolves, the wrapper returns null immediately for status 204. For every other status it reads text, parses nonempty text as JSON, and only then checks res.ok. This ordering means a non-JSON error body can throw SyntaxError before the wrapper creates ApiError.

Use three controlled responses with the same failing status: a JSON ProblemDetails body, an empty body, and a plain-text body. The first produces ApiError using the selected message fields. The second produces ApiError with a status fallback. The third fails during JSON parsing and does not receive the wrapper's normal status-and-details shape.

A learner who tests only JSON business errors may conclude that every error has status and details. The actual source does not provide that guarantee. Network rejection from fetch also propagates before any response handling. A client component therefore receives several possible error shapes unless the wrapper is extended to normalize them deliberately.

The lab includes both malformed JSON and network rejection. It checks identity for the injected network Error, showing that the same object propagates rather than being wrapped. It checks SyntaxError for the non-JSON body. These are useful characterization results, not necessarily the final product experience you would design.

## Message precedence is an API contract

For a non-success JSON response, the wrapper chooses detail, then title, then message, then a status-based fallback. A response containing all three human-readable fields uses detail. ApiError stores the status and defaults details to an empty array when the supplied details value is falsy.

This precedence is useful when comparing middleware ProblemDetails with explicit controller responses. The middleware's detail can be consumed directly, while login's message can serve as a fallback. But a framework model-validation response may expose field errors in a different property. The wrapper does not flatten arbitrary errors objects into its details array in the selected code.

A proposed normalizer should define separate handling for transport failure, unexpected content, model validation, business rules, and unexpected server errors. Do not merely catch every parsing error and pretend it was a domain rejection. The caller may need to know whether retry is safe or whether a request could have succeeded despite an unreadable response.

## A successful response can still fail parsing

The wrapper parses nonempty bodies even when the status is successful. Therefore a successful status with malformed JSON can reject the client operation. This is another unknown-outcome scenario: the server may have completed the mutation, but the client cannot interpret the response.

For Create, retrying blindly after such a failure can create another expense because the selected create path has no request idempotency mechanism. A repair proposal could improve content handling, add request identity, or offer a reconciliation lookup, but each solves a different part of the problem. The course should not suggest that displaying a friendlier parsing message alone prevents duplicate creation.

An empty successful non-204 response yields null. A form page expecting saved.id would then fail later in its own code. This illustrates a distinction between wrapper success and caller contract satisfaction. A wrapper can correctly return its selected value while a caller requires a richer shape.

## Review the twelve assertions as an evidence map

The lab covers JSON success, absent authorization, token and JSON headers, serialized payload, forwarded form object, omitted multipart Content-Type, 204 short-circuit, ProblemDetails precedence and details, empty-error fallback, non-JSON parsing failure, network-error propagation, and token clearing.

The source hash comparison is an additional preservation check rather than one of the twelve behavioral assertions. It demonstrates that this runner did not change client.js during execution. The fake localStorage contains only a synthetic token and is restored afterward. No browser profile or real stored credential is accessed.

A test suite is more useful when its omissions are visible. This lab does not test React effects, downloadReceipt's separate fetch call, storage exceptions, simultaneous requests, abort signals, or response bodies with unusual but valid primitive JSON values. Choose future cases based on an observed contract need rather than assuming the current count measures completeness.

## Design an independent error matrix

Create rows for 204, successful JSON object, successful empty body, failing ProblemDetails, failing message-only JSON, failing empty body, failing plain text, and rejected fetch. For each, predict return value or error type, available status, available details, and whether a server mutation might already have occurred.

Then add a caller column. ExpenseFormPage expects an object with id after save. ExpenseDetailPage's act waits for a mutation then reloads. AuthProvider expects token and user after login. The same wrapper result can have different consequences at each caller, so the matrix should not stop at the transport boundary.

A proposed wrapper change should preserve ordinary successful responses and multipart behavior while improving malformed or unexpected cases. Its acceptance criteria should include message precedence and unknown-outcome wording. A change that catches parsing errors but silently returns an empty object could hide contract failures and create misleading navigation.

## Independent exercises

Exercise NR10-A: complete the eight-row response matrix and identify which cases become ApiError, SyntaxError, a propagated network error, or a normal return. Explain why parsing order matters.

Exercise NR10-B: map each of the twelve executed assertions to one source branch. Name three important client behaviors outside this lab and propose the smallest next test for one of them.

Exercise NR10-C: design a response-normalization proposal for a successful mutation with an unreadable body. Preserve the distinction between failure to interpret the response and proof that the mutation did not happen.
