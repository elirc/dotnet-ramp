# Interview Prep From This Repo

Target: **mid-level software engineering interviews.** Every question below is grounded in
real code here, so your answers come with concrete examples — the thing that separates a
memorized answer from a credible one. For each, three layers:

- **Junior answer** — correct but shallow.
- **Mid adds** — the trade-off / the "why".
- **Senior includes** — risk, alternatives, what it commits you to.

Practice out loud. Cite the file when you can — interviewers love "for example, in this
project I…".

---

## C# / .NET runtime

**Q1. Explain async/await and a deadlock you'd avoid.**
- *Junior:* `await` frees the thread during I/O instead of blocking; you return `Task`.
- *Mid adds:* sync-over-async (`.Result`/`.Wait()`) can deadlock or exhaust the thread pool;
  I keep the chain async end to end — e.g. the receipt upload is async controller→service→disk.
- *Senior includes:* `ValueTask` for hot paths that often complete synchronously (the queue's
  `EnqueueAsync`); the cost of a discarded `ValueTask` (the fire-and-forget enqueue in
  `StartReimbursement`) and when that's acceptable vs. needs awaiting.

**Q2. Singleton vs scoped vs transient. What's the captive dependency bug?**
- *Junior:* singleton = one per app, scoped = per request, transient = per resolution.
- *Mid adds:* a singleton must not depend on a scoped service — it captures the first scope
  forever. Here the singletons (`DataStore`, queue) only depend on other singletons.
- *Senior includes:* the background worker is effectively singleton, so it injects the **root**
  provider and creates a scope per job — that's how you safely use scoped services (and later a
  scoped `DbContext`) off the request thread.

**Q3. `IDisposable`/`using` — where and why?**
- *Junior:* releases unmanaged resources deterministically.
- *Mid adds:* `await using` on a `FileStream` closes the handle even on exception; `using var
  scope` disposes the DI scope.
- *Senior includes:* disposing the scope disposes scoped services in it — critical once a
  `DbContext` lives there.

---

## ASP.NET Core / EF Core / React

**Q4. Walk me through the middleware pipeline and why order matters.**
- *Junior:* requests pass through ordered middleware.
- *Mid adds:* here it's exception-handling → CORS → authN → authZ → controllers; authN must
  precede authZ; exception handling wraps everything.
- *Senior includes:* what breaks if you reorder (authZ-before-authN fails every `[Authorize]`),
  and that the exception middleware first means services can just `throw`.

**Q5. How do you prevent IDOR in an API?**
- *Junior:* check the user's role.
- *Mid adds:* role isn't enough — you must check they own/are-authorized-for *this specific
  resource*. Here `[Authorize(Roles="Manager")]` is coarse; `EnsureManagesOwner` is the real
  check.
- *Senior includes:* enforce it in one layer (service) so every entry point inherits it;
  default-deny switches so new roles fail closed; test the cross-team rejection explicitly.

**Q6. What's an N+1 and how would you spot it here?**
- *Junior:* one query per row instead of one query total.
- *Mid adds:* `Mappings` looks up the employee per expense — free in memory, but under EF Core
  that's N queries; you'd see it in the SQL log.
- *Senior includes:* fix by batch-loading / projecting a join; and the meta-point — *a lookup
  inside a per-item loop is a latent N+1*, recognizable before you ever add a DB.

**Q7. React: why does this effect loop forever / how do you stop stale responses?**
- *Junior:* dependency arrays control when effects run.
- *Mid adds:* an unstable function in deps re-fires every render → wrap `load` in `useCallback`;
  an `active` flag discards late async responses so a slow earlier request can't overwrite a
  newer one.
- *Senior includes:* polling vs push trade-off (the detail page polls; SSE/SignalR would push)
  and cleanup to avoid timer leaks.

---

## Debugging

**Q8. "Users say reimbursement is stuck." How do you debug it?**
- *Junior:* check the logs/code for the reimbursement.
- *Mid adds:* reproduce, then narrow: did the status flip to `Reimbursing`? did the job run
  (log line)? The job has no retry, so one failure = permanently stuck.
- *Senior includes:* this is a *design* gap (at-most-once, no recovery), not just a bug — the
  fix is an outbox + retry + a stuck-detector alert; I'd add the alert first so we *know* next time.

**Q9. "Everything is 401 after login." Where do you look?**
- *Junior:* check the token.
- *Mid adds:* is the `Authorization` header attached? present-but-401 → session invalid.
- *Senior includes:* the killer here — sessions are in-memory, so a backend restart logs
  everyone out while the SPA still holds a dead token; the systemic fix is durable/stateless
  sessions (JWT).

---

## System design (mid-level)

**Q10. Design the expense approval workflow.** (You built one — use it.)
- *Junior:* statuses + endpoints for submit/approve/reject.
- *Mid adds:* a **state machine** as the single source of truth for legal transitions, role-gated,
  with an audit trail; policy checks gating submission (blocking vs warning).
- *Senior includes:* where it breaks at scale (concurrency/TOCTOU, durability), multi-level
  approval as new edges, and how to evolve the contract without breaking clients.

**Q11. Design durable background processing for payments.**
- *Junior:* a queue and a worker.
- *Mid adds:* idempotency (re-running must not double-pay — like the `CanSystemTransition` guard),
  backpressure (bounded channel), error isolation.
- *Senior includes:* the in-memory queue is at-most-once and lost on crash; outbox pattern for
  transactional hand-off; retries + dead-letter; exactly-once-effect via idempotency; how you'd
  test it (crash injection).

**Q12. How would you add a database without breaking the app?**
- *Junior:* add EF Core and a `DbContext`.
- *Mid adds:* hide it behind the existing storage seam (`IDataStore`), keep the API contract
  identical, change the store's lifetime from singleton to scoped.
- *Senior includes:* contract tests run against both store implementations; the N+1 fix; safe
  schema-change discipline; rollback = flip the DI registration.

---

## Code review

**Q13. You're reviewing a PR that sets `expense.Status = Approved` directly. What do you say?**
- *Junior:* "use the state machine."
- *Mid adds:* name it Blocking — it skips legality + audit history; route through the service.
- *Senior includes:* explain the invariant being protected, offer to pair if the needed
  transition isn't in the table, and check whether the missing transition reveals a real gap.

---

## "Tell me about a time…" (grounded in real contributions here)

Use the [tickets](../06-contribution-practice/) as your material:

- **…you improved reliability:** "I found the reimbursement flow could get stuck on a crash
  with no retry; I proposed an outbox + stuck-detector, sequenced behind adding tests first."
- **…you made code testable:** "Date-based policy rules read `DateTime.UtcNow` directly; I
  introduced a clock seam so the rules became deterministic to test."
- **…you caught a security issue in review:** "A PR gated approval only on the Manager role; I
  flagged the IDOR — a manager could approve another team's expense — and we added the
  resource-scope check + a cross-team test."
- **…you kept a change small:** "I wanted to refactor the service while adding a field, but I
  split it: the field shipped in a tiny PR, the refactor became its own RFC."

For each, structure as **Situation → Task → Action → Result**, and name the trade-off you
weighed. That trade-off is what signals mid-level-and-up.

---

## How to practice

1. Pick 3 questions/day; answer out loud at the *senior* layer.
2. For each, open the anchored file and make sure your example is accurate.
3. Record yourself once a week; you'll hear the filler and the hand-waving.
**Self-grade Strong** when you can answer any Q with a specific file/line from this repo and
name at least one alternative or risk.
