# 19. Time, identifiers, and reproducible fixtures

Read [Expense](../../backend/Domain/Expense.cs), [PolicyEngine](../../backend/Services/PolicyEngine.cs), [ExpenseService](../../backend/Services/ExpenseService.cs), [AuthService](../../backend/Security/AuthService.cs), and [BackgroundJobs](../../backend/Background/BackgroundJobs.cs). Several parts of this application create identifiers or read time internally. This chapter designs test seams that preserve business meaning rather than merely making assertions easier to write.

## There is more than one kind of time

ExpenseDate is a calendar date associated with the expense. CreatedAt, UpdatedAt, event timestamps, and session expiry are instants. The queue delay is elapsed waiting time. Treating all of these as one generic clock concept can obscure important differences.

A date policy asks whether a calendar date is future or older than a cutoff relative to a chosen business day. Session expiry asks whether an instant has reached another instant. A worker delay asks whether a duration has elapsed or cancellation occurred. A display formatter asks which local calendar representation the user should see.

A test seam should preserve these distinctions. Passing DateOnly today to a policy core can be clearer than exposing a full clock when only a date is needed. A session service may need a UTC instant. A retry scheduler may need both a persisted next-attempt instant and a monotonic waiting mechanism in the runtime.

## Current internal reads create small gaps

Transition calls Touch, which reads DateTimeOffset.UtcNow, then creates an ExpenseEvent with another UtcNow read. The two values can differ. A test asserting exact equality would encode a contract the current implementation does not intentionally provide.

The background completion path also sets fields and creates an event with time reads. AuthService computes expiry from UtcNow, while Session.IsExpired reads time later. PolicyEngine derives today's UTC date internally. These are separate seams, not one shared injected clock in the current code.

A proposal can capture one operation time and reuse it for related fields. That creates a useful invariant: an accepted transition's UpdatedAt and event time are the same captured instant. But it is a behavior and design choice, so the acceptance criteria should say it explicitly rather than changing tests to assume equality without implementation support.

## A boundary worksheet for midnight

Choose a reference instant just before UTC midnight and an expense date equal to the next UTC day. Before midnight, the policy sees that date as future. After midnight, it is today. A test that computes the fixture date using one clock read and invokes the engine after another can cross that boundary unexpectedly.

The service lab avoids this issue for unrelated tests by choosing yesterday, safely within the nonstale window. That is a sensible fixture technique when the test is about role or queue behavior. It is not a substitute for exact date-boundary tests.

For those tests, introduce a proposed pure evaluation function with explicit today. Use yesterday, today, tomorrow, exactly ninety days ago, and ninety-one days ago. Assert exact codes and severities. This produces a stable truth table independent of the machine's current date.

## Clock advancement should be deliberate

A fake clock can return a fixed instant until the test advances it. Another fake can return a sequence of instants. Use the first when the operation should share one moment and the second when you want to detect multiple reads or ordering assumptions.

For example, a sequence clock returning T for Touch and T plus one millisecond for the event exposes whether the implementation captures once or reads twice. A test should choose that sequence to answer a specific question, not let a random clock create accidental differences.

When testing session expiry, set expiry E and evaluate at E minus one tick, E, and E plus one tick under the proposed seam. Equality must follow the selected greater-than-or-equal contract. A test that sleeps until expiry is slower and less reliable than an explicit clock when the service design permits injection.

## Identifier generation has its own contract

Expense, Receipt, and User initialize identifiers with new GUIDs. The system actor sentinel is Guid.Empty. Background payment references include local generated components. These values serve different purposes: entity identity, sentinel identity, and human-facing reference.

A deterministic fixture can assign explicit init identifiers when constructing domain objects. That avoids asserting against unpredictable values and makes relationships readable. It does not require changing application identifier generation. The source-bound service lab uses generated identities but asserts relationships rather than exact strings, which is another valid strategy.

A proposed command or work identity should not reuse Guid.Empty merely because it is convenient in tests. The sentinel already has meaning for system actor history. Use named deterministic nonempty identifiers for separate entities so accidental equality does not make authorization fixtures pass.

## Stable IDs improve failure diagnosis

A fixture with Owner, Manager, Expense E, Receipt R, and Work W is easier to inspect when each has a distinct explicit identifier. If owner and manager accidentally share an ID, ownership and reporting checks collapse. If two expenses share an ID in a dictionary, one can replace the other before the test begins.

Build a fixture factory that requires identity distinctions or checks them. It should make invalid fixture topology fail early. This is especially important for tests of role-versus-ownership boundaries, where using one all-purpose user object can hide the very distinction under study.

Do not create a giant fixture builder that silently inserts every possible relationship. A minimal explicit fixture is usually more educational: one owner, one manager, one unrelated actor, and only the records needed for the case. Hidden defaults can make a test pass for reasons the learner cannot see.

## Ordering requires a tie contract

ExpenseService.List orders by UpdatedAt descending without an explicit secondary key in the selected expression. If two expenses share the same timestamp, the course should not claim a stable business order beyond what that expression specifies. Dictionary enumeration and later changes can affect equal-key ordering assumptions.

A pagination proposal needs a total order, such as timestamp plus identifier, before it can define reliable continuation. A test should include equal timestamps and verify the proposed tie-breaker. This is a new contract, not a property already guaranteed by the current list method.

A deterministic clock can make ties common in tests, which is useful. Rather than treating ties as unrealistic, use them to expose missing ordering rules. Real operations can share timestamp resolution too, and report consumers should not depend on incidental insertion order.

## Separate random-data tests from deterministic regressions

Generated inputs can explore combinations of amount, category, date, and status. When a generated case fails, preserve the minimal counterexample as a deterministic regression fixture. A future random run might not revisit the same case, and a seed alone can depend on generator version.

For this project, useful generated properties include no human transition outside the declared graph, warning-only policies not making HasBlocking true, and immutable proposed command rejection. Each property needs an independent oracle and a clear input domain. Generating arbitrary enum integers or impossible object graphs without stating their meaning can produce noise rather than useful coverage.

The existing finite lifecycle lab is exhaustive over defined enum values. A random test is not automatically stronger just because it runs many iterations. Choose enumeration when the domain is small and generation when combinations become large or structural.

## Reproducibility includes environment scope

Record the runner command, source hashes, target framework, and whether collaborators are actual or controlled. The .NET labs use disposable .NET 10 projects for selected source files; the app's .NET 8 host is not built by those commands. The JavaScript lab uses Node with fake globals; it is not a browser test.

A useful report lets another learner repeat the same bounded experiment without guessing which dependencies or services were involved. It should also state what was not executed. This is not a disclaimer added after the fact; it is part of the evidence contract that makes a pass interpretable.

## Independent exercises

Exercise NR19-A: classify every selected time value as calendar date, instant, or elapsed duration. Design one explicit seam for each category without forcing them into an unnecessarily broad interface.

Exercise NR19-B: create a fixture topology with distinct owner, manager, expense, receipt, and system actor identities. Add equal UpdatedAt values and specify the ordering contract needed for future pagination.

Exercise NR19-C: design a midnight policy test, an exact session-expiry test, and a sequence-clock transition test. Explain which current lab choices avoid boundaries and which proposed tests intentionally target them.
