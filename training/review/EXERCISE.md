# Blind code review — ExpenseManagement (.netramp)

**Time box: 60 minutes.** Review the repo as if it were a real PR landing on your
team, not a tutorial. Read at least: `backend/Domain/`, `backend/Services/`,
`backend/Security/`, `backend/Background/`, `backend/Controllers/`,
`backend/Storage/DataStore.cs`, and `README.md`'s claims about what the app does.

Do **not** open `training/_answers/review.md` first.

## What to produce

A findings list, each item with:

- **File:line** evidence
- **Severity**: correctness / security / data-integrity / concurrency /
  failure-handling / performance / operability / tests / maintainability
- **Why it matters in production** — one or two sentences, concrete, not generic
- Whether it's a bug in the *current* code, a missing safeguard, or a
  documentation/reality mismatch (README or comment claims something the code
  doesn't actually do)

Rank the list by production severity (worst first). Aim for 15–30 findings —
fewer than that usually means you stopped reading too early; more usually means
you're listing style nits instead of production risk.

## Specific angles worth chasing (don't stop here, but don't skip these)

- The app's own README says "the source is complete but has not been
  compiled/run in this environment." What would you check before trusting that?
- `DataStore` is a `ConcurrentDictionary`-backed singleton and the comment claims
  this "makes individual reads/writes thread-safe... because the background
  worker mutates entities on a separate thread." Is that claim actually true for
  every mutation path you can find, or only for the dictionary's own add/remove
  operations?
- `AuthService.Login`'s comment claims the password check is constant-time to
  avoid timing leaks. Read the actual `||` short-circuit on the line above it.
- Where do uploaded receipt files (and old receipt files replaced by a new
  upload) actually get deleted from disk? Trace every path that removes a
  `Receipt` from `DataStore.Receipts` and check whether the corresponding file
  on disk is cleaned up.
- `docs/upskill/05-quality-engineering/` has files titled `01-testing-strategy.md`
  and `02-writing-tests-here.md`. How many actual test files exist in this repo?
- Look for any place a session (`DataStore.Sessions`) becomes unreachable but is
  never purged.

## Deliverable

Write your findings in your own file (not committed here). When done, compare
against `training/_answers/review.md` using the template in
`training/_answers/COMPARE.md`.
