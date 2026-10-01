# Systematic Debugging

## The method (use it every time)

1. **Reproduce** reliably — exact steps, exact inputs.
2. **Narrow** — which layer? (browser → network → API → service → store → worker). Bisect.
3. **Hypothesize** — one falsifiable guess.
4. **Test cheaply** — a log line, a breakpoint, a single request, not a rewrite.
5. **Fix the root cause** — not the symptom.
6. **Add regression coverage** — a test that would have caught it.

The most important skill is step 2: **decide which side of a boundary the bug is on before
touching code.** The browser network panel + the API console split the system in half for free.

## Tools for this repo

| Tool | Use for |
| --- | --- |
| Browser DevTools → Network | Is the request sent? What status/body came back? Is the token attached? |
| Backend console (`dotnet run`) | `ILogger` output — the worker logs receipt/reimbursement events ([BackgroundJobs L32,68](../../../backend/Background/BackgroundJobs.cs#L32)) |
| Breakpoints in the service | The rules live here; step through `Submit`/`Approve` |
| `ProblemDetails` body | The `detail` + `details[]` tell you exactly which rule failed |
| (future) EF query log | When a DB is added, log generated SQL to catch N+1 |

---

## Scenario 1: "Submit button does nothing / 422"
**Symptom:** clicking Submit shows an error or no state change.
**First question:** did the request reach the server, and what status? (Network panel.)
**Narrowing:** 422 → a Blocking policy flag. Read the `details[]` in the body.
**Likely root causes:** receipt required over $25 ([`PolicyEngine` L50](../../../backend/Services/PolicyEngine.cs#L50)); future date; empty description.
**Regression test:** Recipe 2 in [02](02-writing-tests-here.md).
**Senior lesson:** the API already told you the reason in `details` — read the response before reading the code.

---

## Scenario 2: "Receipt stays "Pending" forever"
**Symptom:** uploaded receipt never flips to Processed.
**First question:** did the job run? (Look for the `Receipt … processed` log line at [BackgroundJobs L32](../../../backend/Background/BackgroundJobs.cs#L32).)
**Narrowing:** no log → job threw (caught at [QueuedHostedService L45-48](../../../backend/Background/QueuedHostedService.cs#L45-L48)) or worker not running; log present but UI stale → polling stopped.
**Likely root causes:** an exception in the job (no retry, so it's stuck); or the SPA stopped polling because status wasn't `Pending` when the effect last ran.
**Regression test:** call `ProcessReceipt(id)` directly and assert status.
**Senior lesson:** "no retry" means one transient failure = permanently stuck. That's a design gap ([03/04](../03-architecture-and-patterns/04-side-effects-async-and-reliability.md)), not just a bug.

---

## Scenario 3: "Manager gets 403 approving their own report's expense"
**Symptom:** legitimate approval rejected.
**First question:** is the expense's owner *actually* a direct report? (`owner.ManagerId == manager.Id`?)
**Narrowing:** breakpoint at [`EnsureManagesOwner` L200](../../../backend/Services/ExpenseService.cs#L200); inspect both ids.
**Likely root causes:** seeder linked the employee to a different manager; or the expense isn't in `Submitted` (so the transition, not the auth, fails).
**Regression test:** Recipe 4 (positive case: same-team approve succeeds).
**Senior lesson:** 403 here can come from *two* places (auth vs. illegal transition) — distinguish before fixing.

---

## Scenario 4: "Everything returns 401 after login"
**Symptom:** login succeeds but subsequent calls are unauthorized.
**First question:** is `Authorization: Bearer …` on the failing requests? (Network panel headers.)
**Narrowing:** header missing → token not stored/attached ([client L22-23](../../../frontend/src/api/client.js#L22-L23)); header present but 401 → session invalid/expired ([AuthService L41-52](../../../backend/Security/AuthService.cs#L41-L52)); or the backend restarted (in-memory sessions wiped!).
**Likely root cause (most common here):** **backend restarted** → all sessions gone, but the SPA still holds a now-dead token in `localStorage`.
**Fix/Workaround:** log out/in; longer term, detect 401 globally and redirect to login.
**Senior lesson:** in-memory sessions make "restart = logout for everyone" — obvious once known, baffling until then.

---

## Scenario 5: "Frontend can't reach the API at all"
**Symptom:** every call fails with a network/proxy error, not a clean status.
**First question:** is the backend running on `:5085`? (It's the Vite proxy target — [vite.config.js](../../../frontend/vite.config.js).)
**Narrowing:** backend down → proxy error; backend up but CORS error → origin mismatch ([Program.cs L37-41](../../../backend/Program.cs#L37-L41)).
**Likely root cause:** backend not started, or started on a different port than the proxy expects.
**Senior lesson:** two processes = two things that can be down. Check both terminals first; don't debug app code for an infra problem.
