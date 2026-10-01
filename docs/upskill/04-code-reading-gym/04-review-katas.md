# Review Katas

Each is a fake PR against this repo. Decide your findings and severity before reading the
expected answers. Severity: **Blocking** (must fix), **Important** (should fix), **Optional**
(nit/preference).

---

## Kata 1: "Add a quick admin endpoint"
**Author intent:** let finance edit any expense's amount.
**Fake diff:** adds `[HttpPut("{id}/amount")]` in the controller that does
`_store.Expenses[id].Amount = req.Amount;` directly.
**Resembles:** [`ExpensesController`](../../../backend/Controllers/ExpensesController.cs).
**Expected findings:** *Blocking* — bypasses the service, no ownership/state checks, mutates a
possibly non-editable/terminal expense, no policy refresh, no audit history. **Good comment:**
> This writes to the store directly, skipping every guard in `ExpenseService`. Editing should
> go through a service method that checks `IsEditable` and re-runs policy. Also: should finance
> be able to edit amounts at all? That changes the approval's meaning.

---

## Kata 2: "Return the user object on login"
**Fake diff:** `return Ok(user);` instead of `user.ToResponse()`.
**Expected:** *Blocking* — leaks `PasswordHash`/`PasswordSalt`. **Comment:** map to `UserResponse`.

---

## Kata 3: "Skip the receipt check for managers"
**Fake diff:** in `PolicyEngine`, wrap RECEIPT_REQUIRED in `if (actor.Role != Manager)`.
**Expected:** *Important/Blocking* — `PolicyEngine` has no business knowing the actor; it
evaluates the *expense*. Coupling policy to roles breaks its purity and testability. Also
managers don't submit their own expenses through that path typically. Push role logic up.

---

## Kata 4: "Make reimbursement instant"
**Fake diff:** `StartReimbursement` sets status straight to `Reimbursed` and stamps the ref
synchronously; removes the job.
**Expected:** *Important* — loses the async model the task requires; but actually *more*
reliable than today's crash window. Real review answer: discuss the trade-off. If "instant"
is wanted, fine — but then remove the `Reimbursing` state cleanly and update the state
machine + UI polling. Half-removing it leaves dead states.

---

## Kata 5: "Add caching to the list endpoint"
**Fake diff:** a `static Dictionary<Guid,List<ExpenseResponse>>` cache keyed by user id, never
invalidated.
**Expected:** *Blocking* — stale data after any mutation; and a `static` shared cache of
*authorized* results is a data-leak risk if keys collide. Cache invalidation + scoping needed,
or don't cache an already in-memory read.

---

## Kata 6: "Allow .gif receipts"
**Fake diff:** adds `["image/gif"] = ".gif"` to the allowlist.
**Expected:** *Optional/Important* — mechanically fine and follows the pattern. Ask: do we
want gifs? Does the (future real) OCR support them? Low risk; approve with the question.

---

## Kata 7: "Log the bearer token for debugging"
**Fake diff:** `_logger.LogInformation("token={Token}", token);` in the auth handler.
**Expected:** *Blocking* — logging credentials. Tokens in logs = account takeover. Never.

---

## Kata 8: "Parallelize the worker"
**Fake diff:** the worker now spawns `Task.Run(() => work(...))` per job without awaiting.
**Expected:** *Important* — drops error isolation (unobserved exceptions), removes
backpressure, and amplifies the data-race risk on shared entities. If concurrency is wanted,
bound it (e.g. a `SemaphoreSlim`) and keep per-task try/catch + scope.

---

## Kata 9: "Tidy up: remove unused `NextStates`"
**Fake diff:** deletes [`ExpenseStateMachine.NextStates` L47-48](../../../backend/Domain/ExpenseStateMachine.cs#L47-L48).
**Expected:** *Optional* — it is currently unused (confirm with a search). Drive-by deletion
is fine if truly unused, but is it worth the diff noise in an unrelated PR? Prefer a separate
"dead code" PR. Teaches: avoid scope-creep in reviews.

---

## Kata 10: "Add `[AllowAnonymous]` to reports for a public dashboard"
**Fake diff:** `[AllowAnonymous]` on `ReportsController`.
**Expected:** *Blocking* — `Summarize` calls `List(actor, …)`; with no actor, `RequireUser`
throws, or worse if "fixed" by defaulting the actor it would leak everyone's spend. Public
reporting needs a separate, deliberately-aggregated, non-scoped endpoint — a design change,
not an attribute.
