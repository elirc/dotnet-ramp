# Maintainer Communication

The goal: get unblocked **without outsourcing your thinking.** Show what you tried and what
you concluded; ask a specific question.

## Asking a good question

❌ "How does auth work here?"
✅ > I traced login → `TokenAuthenticationHandler` → `UserContext`. I see role checks via
`[Authorize(Roles)]` *and* `EnsureManagesOwner` in the service. Am I right that the attribute
is a coarse gate and the service does the real resource check? Trying to add an endpoint and
want to put the authz in the right place.

The second shows the work, names anchors, and asks one answerable thing.

## Reporting a bug (minimal repro)

```markdown
**What I did:** logged in as employee@demo.com, created a $30 Meals expense, clicked Submit.
**Expected:** submitted.
**Actual:** 422 with detail "A receipt is required for amounts over $25.00".
**Notes:** is this intended? (Looks like RECEIPT_REQUIRED in PolicyEngine L50.) If so, the UI
could disable Submit until a receipt is attached.
```

A good report includes exact steps, expected vs actual, and your hypothesis with an anchor.

## Templates

**Feature request:**
> **Problem:** <user-facing pain>. **Proposed:** <sketch>. **Alternatives:** <one>.
> **Willing to implement:** yes/no.

**Responding to review (disagreeing respectfully):**
> Good point on X — fixed. On Y, I left it because <reason + anchor>; happy to change if you'd
> prefer Z. Trade-off as I see it: <one line>.

**Responding to review (accepting):**
> Done in <commit>. You're right that <restate the why> — I missed the <case>.

## Etiquette specific to this repo

- There's no `CONTRIBUTING.md` yet — propose changes as small PRs and ask about conventions
  rather than assuming. (Writing that file is [Ticket 18](../06-contribution-practice/01-good-first-tickets.md).)
- Keep PRs focused; the "go through the service / state machine" rule is the one invariant
  maintainers will care most about.
- Don't open a giant refactor unsolicited — propose it as an RFC first.

## Disagreeing well (the senior move)

State the trade-off, not the verdict. "I'd prefer A because it reduces blast radius, but B is
fine if we add a test for the edge case" invites a decision; "A is correct, B is wrong" invites
a fight. Bring evidence (an anchor, a benchmark, a failing test), then defer to ownership.
