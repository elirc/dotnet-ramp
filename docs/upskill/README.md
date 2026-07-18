# Upskill: The Expense Management Training Lab

This is a self-directed curriculum that turns **this** repository — a small ASP.NET Core
(C#) + React expense-management system — into a training lab. It teaches two things at
once:

1. **This codebase specifically** — where things live, how a request flows, why each
   pattern was chosen.
2. **Transferable engineering judgment** — runtime fundamentals, framework mental models,
   architecture, testing, debugging, performance, security, code review, and how to
   contribute to a real project.

## Repo identity (read this first)

This is a learning clone of Ramp / Expensify / Brex expense workflows. An **employee**
creates an expense, attaches a receipt, and submits it; a **manager** approves or rejects
it; **finance** reimburses it (payment completes asynchronously). The backend is an
ASP.NET Core 8 Web API with **zero NuGet dependencies** — everything (auth, hashing,
background queue, JSON) uses the bundled framework, by design, so the moving parts are
visible rather than hidden in libraries. Persistence is an **in-memory store** (a
singleton of `ConcurrentDictionary`s) that resets on restart; this keeps the focus on the
domain (a state machine, a policy engine, role-scoped authorization) instead of a
database. The frontend is a Vite + React SPA that talks to the API over `fetch` with a
bearer token. The whole thing is intentionally readable in an afternoon — which is exactly
what makes it a good lab.

> ⚠️ **Status caveat (verify before trusting):** at the time these docs were written the
> code had **not been compiled or run** in this environment (no .NET SDK installed). The
> code is complete and written to be correct, but treat "it builds / it runs" as
> *unverified* until you run it yourself. See [08-reference/verification-log.md](08-reference/verification-log.md).

## Who this is for

A junior engineer who wants to reach mid-level (and start thinking like a senior). It also
works for a mid-level engineer who is new to ASP.NET Core, React, or to this repo.

## The four stages

| Stage | You can… | Driving question |
| --- | --- | --- |
| **Orientation** | Run it, name the parts, trace one feature end to end | "How does this work?" |
| **Junior** | Make small, safe changes using existing patterns | "How do I make it work?" |
| **Mid-level** | Design cross-layer changes, write tests, debug, review PRs | "Is this the right pattern?" |
| **Senior** | Critique architecture, weigh risk, plan migrations | "What does this commit us to, who pays, how do we cut risk?" |

That last shift is the whole point: a junior asks *how do I make it work?*; a mid-level
asks *is this the right pattern?*; a senior asks *what does this commit us to, who pays the
cost, and how do we reduce the blast radius?*

## Recommended paths

- **Brand-new junior:** [00-fast-track](00-fast-track.md) → [01-codebase-cartography](01-codebase-cartography/) →
  [02-stack-and-language-mastery](02-stack-and-language-mastery/) → [04-code-reading-gym](04-code-reading-gym/) →
  [06-contribution-practice/01-good-first-tickets](06-contribution-practice/01-good-first-tickets.md).
- **Junior who knows C#/React:** skim 01, do [03-architecture-and-patterns](03-architecture-and-patterns/) →
  [05-quality-engineering](05-quality-engineering/) → mid-level tickets.
- **Mid-level new to repo:** [00-fast-track](00-fast-track.md) → [01/05-key-flows](01-codebase-cartography/05-key-flows.md) →
  [03/06-architecture-critique](03-architecture-and-patterns/06-architecture-critique.md) → [08/risk-register](08-reference/risk-register.md).
- **Senior doing a review:** [03/06-architecture-critique](03-architecture-and-patterns/06-architecture-critique.md) →
  [08/risk-register](08-reference/risk-register.md) → [06/03-senior-build-projects](06-contribution-practice/03-senior-build-projects.md).
- **Interview prep:** [07/04-interview-prep-from-this-repo](07-career-and-collaboration/04-interview-prep-from-this-repo.md).

## Time budgets

- **A weekend:** [00-fast-track](00-fast-track.md) + trace 2 flows + one good-first ticket.
- **Two weeks:** modules 00–04 + write 3 tests + ship 3 tickets.
- **Eight weeks:** the whole suite, finishing 1 senior build project + an RFC.
- **Ongoing:** the risk register and senior katas are a backlog you can pull from forever.

## Conventions used throughout

- **File anchors** look like [`ExpenseService.cs` L114-130](../../backend/Services/ExpenseService.cs#L114-L130).
  Every claim about the code points to one. If it doesn't, treat it as opinion.
- **Illustrative code** is always labelled `// Illustrative — not from this repo`. Real
  excerpts are short and anchored.
- **Drills** ask you to do something, then self-grade **Basic / Solid / Strong**.
- **Verification Notes** record what was actually checked vs. assumed. Distrust anything
  marked *investigate* or *possible risk* until you confirm it.

Start at [00-fast-track.md](00-fast-track.md).
