# 09. Session resolution and problem-detail boundaries

Read [AuthService](../../backend/Security/AuthService.cs), [PasswordHasher](../../backend/Security/PasswordHasher.cs), [UserContext](../../backend/Security/UserContext.cs), [AuthController](../../backend/Controllers/AuthController.cs), and [exception middleware](../../backend/Middleware/ExceptionHandlingMiddleware.cs). This chapter follows identity resolution and error presentation through their current boundaries. It uses synthetic credentials in proposed fixtures and does not inspect or print stored credential values.

## Password verification is one step in login

AuthService finds a user by email and returns no result when the user is absent or password verification fails. The store's email lookup uses ordinal case-insensitive comparison. The service short-circuits when the user is missing, so the selected login path does not perform the same password work in that case.

PasswordHasher derives a key using PBKDF2 with SHA-256, one hundred thousand iterations, a sixteen-byte random salt for new hashes, and a thirty-two-byte derived key. Verification decodes the stored salt and expected key, derives an actual key of the expected length, and compares bytes using FixedTimeEquals.

These are source facts about the chosen implementation. They are not a claim that the entire login endpoint has constant-time behavior or meets every production security requirement. The fixed-time comparison is one operation within a larger path containing lookup, branching, decoding, and key derivation. A course should preserve that scope instead of turning a function-level property into an endpoint guarantee.

A bounded password test could hash one synthetic password twice, verify both, reject a different password, and check that the generated salts differ. Such a test would establish basic functional behavior, not measure resistance to every attack. No real password or derived credential needs to appear in its report.

## Opaque sessions are stored server-side

On successful login, the service generates a token from thirty-two random bytes encoded as Base64 and stores a Session in the in-memory dictionary. The session contains the user identifier and an expiry derived from current UTC time plus the configured lifetime, which defaults to eight hours.

The token is opaque to the client in this design. The server looks it up rather than parsing a signed claim set from it. This makes logout straightforward in the selected implementation: remove the token's dictionary entry. It also means a process restart loses the in-memory session map unless another persistence mechanism is introduced.

Do not describe a future signed-token design as a drop-in replacement merely because both use a bearer header. Revocation, membership updates, expiry validation, and storage have different tradeoffs. For this course, the important contract is the current lookup and removal behavior, not choosing a universal authentication technology.

## Validation resolves the current user

ValidateToken first looks up the token. If its Session reports expired, the service removes that session and returns no user. Otherwise it looks up the current User by the stored identifier. A session pointing to a missing user therefore does not return a stale embedded user object, because the session stores only the identifier.

Session.IsExpired compares current UTC time with ExpiresAt using greater-than-or-equal. Equality is expired. Because time is read inside the property, deterministic equality testing is harder than it would be with an injected clock or explicit now parameter. A proposed seam should be small enough to preserve the current behavior while enabling exact boundary fixtures.

A test plan should distinguish unknown token, expired token, valid token with present user, valid token with missing user, and token removed by logout. These cases may all lead to an unauthenticated request later, but they exercise different state and cleanup behavior inside the service.

## Request context is another lookup boundary

UserContext reads the NameIdentifier claim from the current HttpContext and tries to parse it as a GUID. Missing or unparsable values yield no UserId. RequireUser throws ForbiddenException if no identity is available, then looks up the user in DataStore and throws another forbidden exception if the user no longer exists.

This context object does not itself verify a password or token. It assumes the surrounding authentication pipeline supplied the principal, then maps its identifier to current domain data. A direct UserContext test can establish behavior for a constructed ClaimsPrincipal without proving the real bearer handler creates only trustworthy claims.

Likewise, a service test that passes a User object directly does not prove request authentication. The authority matrix from chapter one remains valuable, but its evidence boundary starts after actor resolution. A complete HTTP test must exercise the handler, authorization attributes, context lookup, service checks, and response mapping in their configured order.

## Controller results and thrown exceptions differ

AuthController returns an explicit Unauthorized result with a generic invalid-email-or-password message when Login returns no result. Logout requires authorization, extracts a bearer token when the header has the expected prefix, removes it, and returns no content. The me action requires authorization and maps RequireUser's result into a response DTO.

Those explicit controller results are not the same as exceptions handled by the custom middleware. A test should not assume every authentication failure uses application/problem+json merely because the middleware exists. Framework authentication challenges, model validation, controller-returned results, and caught exceptions can have different response paths.

Trace the path for each case before asserting a response shape. This is especially useful when a client displays only a message field but another path returns ProblemDetails with Detail. A robust client contract should be based on observed endpoint responses or a deliberately standardized API design.

## The middleware maps exception categories

The selected middleware catches exceptions from the next delegate and maps NotFoundException to 404, ForbiddenException to 403, DomainException to 422, and other exceptions to 500. For unexpected exceptions it logs the exception and returns a generic detail. For the known categories it uses the exception message. DomainException details are added to the problem extensions when present.

This ordering matters if exception types have inheritance relationships. The more specific categories appear before the general domain category. A table-driven middleware test should use representative exceptions and assert status, title, content type, detail behavior, and optional details extension.

The selected implementation does not show a response-started guard before writing the problem response. A proposed test can start a response and then throw to characterize what happens, but the course has not executed that host scenario. Do not claim the middleware can always replace an already-started response cleanly merely because it catches Exception.

## Preserve diagnostic usefulness without exposing internals

The generic 500 detail avoids returning the unexpected exception message in this mapping. The server log still receives the exception. A test can use a synthetic sensitive-looking marker in an exception message and assert it is absent from the serialized response while a controlled logger records the error category.

That does not justify logging real credentials or entire request bodies. A diagnostic proposal should use correlation identifiers, operation names, and sanitized failure categories. The educational report should contain fixture identifiers and outcomes, not token values or password material.

For known domain exceptions, the selected middleware exposes their messages. Authors of those messages should therefore treat them as client-facing content. A future error-code contract can separate stable machine identifiers from human wording, but that is an API evolution proposal rather than a current feature inferred from ProblemDetails alone.

## Independent exercises

Exercise NR09-A: trace unknown, expired, removed, and valid tokens through AuthService. Explain which cases mutate session storage and why fixed-time password comparison does not establish constant-time login behavior.

Exercise NR09-B: construct UserContext fixtures for absent claim, malformed GUID, valid missing user, and valid present user. State which authentication claim those fixtures do not prove.

Exercise NR09-C: build a response-path table for explicit login failure, a domain exception, and an unexpected exception. Propose a bounded middleware test including a synthetic marker and identify the response-started scenario as additional unexecuted evidence.
