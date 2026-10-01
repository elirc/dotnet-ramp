# Code Review Mindset

## The five questions, in order

1. **Does it work?** Does it do what the PR says?
2. **Is it correct?** Edge cases, error paths, concurrency, auth.
3. **Will it stay correct?** Tests, clarity, does it trap the next person?
4. **Does it fit?** Follows this repo's patterns (service-layer authz, state machine, DTOs)?
5. **Is it kind to maintainers?** Small, focused, well-described, easy to revert?

Stop at the first that fails — don't bikeshed style on a PR with an auth hole.

## This repo's review checklist

- [ ] State changes go through `Transition` / the service, not direct mutation. ([pattern](../03-architecture-and-patterns/05-pattern-catalog.md))
- [ ] New endpoints check **resource** ownership, not just `[Authorize(Roles=…)]`. ([IDOR](../03-architecture-and-patterns/03-validation-auth-and-permissions.md))
- [ ] Responses are DTOs; no sensitive field leaks.
- [ ] New enum/role value: every authorization `switch` updated, still fail-closed.
- [ ] Background work: resolves scoped services from `sp`, doesn't capture them.
- [ ] No secrets/tokens in logs.
- [ ] PR is focused — no unrelated drive-by refactors. ([Kata 9](../04-code-reading-gym/04-review-katas.md))

## Severity language (use it explicitly)

- **Blocking:** must fix before merge (correctness/security).
- **Important:** should fix (maintainability, missing test).
- **Optional / nit:** preference; say so, and let the author decide.

## Good vs. bad review comments

❌ "This is wrong." / "Why didn't you use a state machine?"
✅ > **Blocking:** this sets `expense.Status` directly, which skips the legality check and the
audit history in `Transition` ([L169](../../../backend/Services/ExpenseService.cs#L169)). Can
you route it through a service method? Happy to pair if the transition you need isn't in the
table yet.

The good comment: names severity, explains *why* with an anchor, and offers a path forward.

## Being reviewed well

- Keep PRs small; one concern each.
- Pre-empt questions in the description ("I didn't add X because…").
- Respond to every comment (fix, or explain why not). Disagree with reasons, not ego.
- Don't expand scope mid-review; file a follow-up instead.
