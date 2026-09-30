# 14. Auth bootstrap races and local session recovery

Read [AuthContext](../../frontend/src/auth/AuthContext.jsx), [App](../../frontend/src/App.jsx), and [client.js](../../frontend/src/api/client.js). The server-side session chapter explained token lookup and current-user resolution. This chapter examines how the browser decides whether to show authenticated routes and what happens when asynchronous bootstrap work overlaps another session action.

## Three pieces of state are related but distinct

AuthProvider stores user and loading in React state while tokenStore keeps the bearer token in localStorage. These are not one atomic state object. A request reads the token independently, and route protection reads user and loading through context.

At startup, loading begins true and user begins null. The bootstrap effect checks whether a token exists. If absent, it stops loading. If present, it requests the current user from the server, sets user if the effect remains active, clears the token on any caught failure, and stops loading if active.

RequireAuth shows loading while bootstrap is pending, redirects to login when user is absent, and otherwise renders children. This is client navigation behavior. The server still authorizes every protected request independently; a rendered route is not proof that the token remains valid.

## The active flag protects some effects, not all

The bootstrap effect sets active false during cleanup. Successful user assignment and final loading assignment check active. The catch block clears tokenStore without checking active. Therefore a late failed bootstrap can still mutate storage after the effect has become inactive under a controlled lifecycle sequence.

A test should distinguish React state updates from external storage writes. An active flag can prevent setUser after unmount while leaving localStorage mutation unaffected. Calling the whole effect canceled would overstate what the flag does.

Use a deferred current-user request. Begin bootstrap with a synthetic token, unmount the provider or trigger the relevant lifecycle cleanup, then reject the request. Observe whether the token is cleared. This is a proposed component test; the actual-source client lab does not mount AuthProvider and cannot establish this effect behavior.

## A newer login can overlap an older bootstrap

Consider a bootstrap request made with token A. Before it settles, another login succeeds and stores token B with user B. If the old bootstrap later fails, its unconditional catch clears storage. If it succeeds while the effect is still active, it can set user based on the old response. The exact reachable UI sequence depends on how the provider and login route are mounted, so a controlled test must establish that lifecycle rather than assume it.

The broader design issue is request identity. The old request should not overwrite a newer session decision merely because it finishes later. A generation counter, token comparison, or abort-and-ignore strategy can bind bootstrap results to the session version that initiated them.

A token comparison proposal should avoid printing tokens in diagnostics. The test can use synthetic values and assert equality internally, while logs record only generation identifiers or case names. The goal is to preserve causal ownership of state, not expose credentials during debugging.

## Login commits browser state in order

The login function awaits the API response, stores res.token, sets user from res.user, and returns that user. If token storage itself throws, setUser is not reached through this sequence. The selected code does not catch storage exceptions locally.

A browser environment can have storage availability constraints, but this course does not claim a particular browser policy was tested. A controlled unit fixture can make setItem throw and observe the function's result. The product design can then choose whether to show an explicit persistence error, use session-only memory, or fail login safely under its requirements.

A successful API response also needs the expected token and user shape. The fetch wrapper does not validate those fields. A proposed response schema or integration test should establish that the controller and client agree. Treating any successful JSON object as a valid authenticated session would hide a contract error.

## Logout prioritizes local clearing after an attempt

The selected logout function attempts the server request and ignores its failure, then clears local token storage and sets user null. This gives the browser a local sign-out path even when the server request fails. It does not prove the server-side session was revoked in that failure case.

A useful user-facing contract can distinguish signing out of this browser from revoking a session on the server. For a simple demo, local clearing may be acceptable. A stronger requirement might retry revocation or offer session management, but it should not prevent the user from leaving an authenticated UI indefinitely because the network is unavailable.

The order matters if the logout request hangs rather than rejects. The function awaits it before clearing local state, and the selected API wrapper provides no explicit timeout or abort parameter. A controlled promise that never settles can therefore keep this sequence pending. A proposed design should state a bounded local logout policy rather than rely on unspecified network timing.

## Expiry after bootstrap is a different event

Bootstrap validates the stored token once when the provider effect runs. The selected context does not show a periodic current-user refresh or a centralized reaction that clears user on every later 401. A user can therefore remain represented in context while a later server request rejects an expired session, depending on page behavior.

This is not unusual by itself; applications can handle expiry through request-level recovery. The important question is where that recovery contract lives. The current wrapper creates ApiError for parseable error responses but does not notify AuthProvider automatically. Individual pages usually display messages.

A proposed session-expiry coordinator could clear the appropriate session generation and navigate to login after a confirmed authentication rejection. It must avoid clearing a newer login because an older request failed late. The bootstrap race and request-error race are the same causal problem at different entry points.

## Route protection should be tested separately from data authorization

A RequireAuth test can verify loading display, redirect when user is absent, and child rendering when user exists. It cannot prove that hidden routes are inaccessible through HTTP or that a user has the right role for every action. Those guarantees belong to server authorization and service rules.

A useful integration plan combines a route harness with controlled API responses, then a separate host test for actual authorization. Keeping these tests distinct makes failures easier to interpret. A broken redirect should not be mistaken for a server permission defect, and a hidden button should not be accepted as permission enforcement.

## Recovery state should tell the truth

A proposed auth state model might distinguish bootstrapping, authenticated, locally signed out, expired, and temporarily unable to validate. The current context uses only user and loading, with token clearing on any bootstrap failure. That can collapse a transient server outage and an invalid session into the same local outcome.

Whether to retain a token after a transient failure is a product and security design decision. The course does not prescribe a universal answer. It asks you to define the observable behavior, retry rules, and generation ownership so the decision is consistent and testable.

## Independent exercises

Exercise NR14-A: draw bootstrap success, bootstrap failure after cleanup, and old-bootstrap completion after a newer login. Mark React state writes and storage writes separately.

Exercise NR14-B: design a generation-based recovery contract for late 401 responses without exposing token values in logs. Include a newer successful login before the old response arrives.

Exercise NR14-C: compare local logout with confirmed server revocation and a hanging logout request. Specify a bounded user experience and separate route-protection tests from server-authorization tests.
