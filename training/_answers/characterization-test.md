# Answer — Characterization test for `ExpenseStateMachine`

## The full legal-transition table (as of `main` @ `c5ef2a7`)

`CanTransition(from, to, role)` is `true` only for these 5 triples (108 total
combinations checked, 103 are `false`):

| from | to | role |
|---|---|---|
| Draft | Submitted | Employee |
| Rejected | Submitted | Employee |
| Submitted | Approved | Manager |
| Submitted | Rejected | Manager |
| Approved | Reimbursing | Finance |

`CanSystemTransition(from, to)` is `true` for the 6 edges present in the table
(role-restricted or not), `false` otherwise:

| from | to |
|---|---|
| Draft | Submitted |
| Rejected | Submitted |
| Submitted | Approved |
| Submitted | Rejected |
| Approved | Reimbursing |
| Reimbursing | Reimbursed |

(`Reimbursing → Reimbursed` has an empty role array, so `CanTransition` is
`false` for it under *every* role — it can only ever fire via
`CanSystemTransition`, which is what `BackgroundJobs.ProcessReimbursement`
uses.)

`IsTerminal(status)` is `true` only for `Reimbursed`.

`NextStates(from)`:

| from | NextStates |
|---|---|
| Draft | `[Submitted]` |
| Rejected | `[Submitted]` |
| Submitted | `[Approved, Rejected]` |
| Approved | `[Reimbursing]` |
| Reimbursing | `[Reimbursed]` |
| Reimbursed | `[]` (no entry in `Transitions` at all) |

## Reference test

```csharp
using ExpenseManagement.Api.Domain;
using Xunit;

namespace ExpenseManagement.Api.Tests.Domain;

public class ExpenseStateMachineCharacterizationTests
{
    // The complete set of (from, to, role) triples CanTransition currently
    // allows. Any change to ExpenseStateMachine that adds, removes, or
    // reassigns a role for an edge must show up as a failure here.
    public static readonly HashSet<(ExpenseStatus From, ExpenseStatus To, UserRole Role)> LegalTransitions = new()
    {
        (ExpenseStatus.Draft, ExpenseStatus.Submitted, UserRole.Employee),
        (ExpenseStatus.Rejected, ExpenseStatus.Submitted, UserRole.Employee),
        (ExpenseStatus.Submitted, ExpenseStatus.Approved, UserRole.Manager),
        (ExpenseStatus.Submitted, ExpenseStatus.Rejected, UserRole.Manager),
        (ExpenseStatus.Approved, ExpenseStatus.Reimbursing, UserRole.Finance),
    };

    public static readonly HashSet<(ExpenseStatus From, ExpenseStatus To)> LegalSystemTransitions = new()
    {
        (ExpenseStatus.Draft, ExpenseStatus.Submitted),
        (ExpenseStatus.Rejected, ExpenseStatus.Submitted),
        (ExpenseStatus.Submitted, ExpenseStatus.Approved),
        (ExpenseStatus.Submitted, ExpenseStatus.Rejected),
        (ExpenseStatus.Approved, ExpenseStatus.Reimbursing),
        (ExpenseStatus.Reimbursing, ExpenseStatus.Reimbursed),
    };

    private static IEnumerable<ExpenseStatus> AllStatuses => Enum.GetValues<ExpenseStatus>();
    private static IEnumerable<UserRole> AllRoles => Enum.GetValues<UserRole>();

    [Fact]
    public void CanTransition_matches_exactly_the_known_legal_set()
    {
        foreach (var from in AllStatuses)
        foreach (var to in AllStatuses)
        foreach (var role in AllRoles)
        {
            bool expected = LegalTransitions.Contains((from, to, role));
            bool actual = ExpenseStateMachine.CanTransition(from, to, role);

            Assert.True(expected == actual,
                $"CanTransition({from}, {to}, {role}) expected {expected} but was {actual}. " +
                "If this is an intentional change, update LegalTransitions above " +
                "AND re-check ExpenseStateMachine's class doc comment diagram.");
        }
    }

    [Fact]
    public void CanSystemTransition_matches_exactly_the_known_legal_set()
    {
        foreach (var from in AllStatuses)
        foreach (var to in AllStatuses)
        {
            bool expected = LegalSystemTransitions.Contains((from, to));
            bool actual = ExpenseStateMachine.CanSystemTransition(from, to);
            Assert.True(expected == actual,
                $"CanSystemTransition({from}, {to}) expected {expected} but was {actual}.");
        }
    }

    [Theory]
    [InlineData(ExpenseStatus.Draft, false)]
    [InlineData(ExpenseStatus.Submitted, false)]
    [InlineData(ExpenseStatus.Approved, false)]
    [InlineData(ExpenseStatus.Rejected, false)]
    [InlineData(ExpenseStatus.Reimbursing, false)]
    [InlineData(ExpenseStatus.Reimbursed, true)]
    public void IsTerminal_matches_known_set(ExpenseStatus status, bool expected) =>
        Assert.Equal(expected, ExpenseStateMachine.IsTerminal(status));

    [Theory]
    [InlineData(ExpenseStatus.Draft, new[] { ExpenseStatus.Submitted })]
    [InlineData(ExpenseStatus.Rejected, new[] { ExpenseStatus.Submitted })]
    [InlineData(ExpenseStatus.Submitted, new[] { ExpenseStatus.Approved, ExpenseStatus.Rejected })]
    [InlineData(ExpenseStatus.Approved, new[] { ExpenseStatus.Reimbursing })]
    [InlineData(ExpenseStatus.Reimbursing, new[] { ExpenseStatus.Reimbursed })]
    [InlineData(ExpenseStatus.Reimbursed, new ExpenseStatus[0])]
    public void NextStates_matches_known_set(ExpenseStatus from, ExpenseStatus[] expected) =>
        Assert.Equal(expected.ToHashSet(), ExpenseStateMachine.NextStates(from).ToHashSet());
}
```

`BUILD/RUN: VERIFIED` — this exact test (against a standalone `dotnet new
xunit` project referencing only `Domain/ExpenseStateMachine.cs` + `Enums.cs`,
SDK 10.0.302, target `net10.0`) was compiled and run as part of this batch:
`Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14`. When wiring this into
the real repo, scaffold the test project at `backend/
ExpenseManagement.Api.Tests` with a project reference to `ExpenseManagement.Api
.csproj` instead (so it picks up the actual `net8.0` target and namespaces) and
confirm `dotnet test` still passes there — the standalone copy used for this
verification is not a substitute for running it against the real project once
that scaffold exists.

## Confirming it catches incident 001 (verified)

Applying incident 001's one-line change
(`[ExpenseStatus.Reimbursing] = new[] { UserRole.Finance }` added under
`[ExpenseStatus.Submitted]`) to the same standalone copy and re-running
`dotnet test` produces exactly the expected break:

```
Failed CharTest.ExpenseStateMachineCharacterizationTests.CanTransition_matches_exactly_the_known_legal_set
  CanTransition(Submitted, Reimbursing, Finance) expected False but was True.
Failed CharTest.ExpenseStateMachineCharacterizationTests.CanSystemTransition_matches_exactly_the_known_legal_set
  CanSystemTransition(Submitted, Reimbursing) expected False but was True.
Failed CharTest.ExpenseStateMachineCharacterizationTests.NextStates_matches_known_set(from: Submitted, ...)
  Expected: [Approved, Rejected]  Actual: [Approved, Rejected, Reimbursing]

Failed! - Failed: 3, Passed: 11, Skipped: 0, Total: 14
```

Three of the four characterized members catch it independently (`CanTransition`,
`CanSystemTransition`, and `NextStates` all disagree with the incident diff);
only `IsTerminal` is unaffected, which makes sense — the incident doesn't touch
which state is terminal. That's the signal a table-driven, exhaustive
characterization test gives you that a handful of example-based tests would
likely miss (an example-based suite only fails if someone happened to write a
test for that specific edge).

## What to add if the state machine grew a 7th status

The exhaustive double/triple loop stays cheap up to maybe a dozen statuses
(12×12×3 = 432 cases, still milliseconds). Past that, or once transitions start
depending on more than `(from, to, role)` — e.g. incident 001's "but only for
pre-approved vendors" framing, which the *current* table structure can't even
express — the right next step is a property-based test (e.g. FsCheck) generating
random `(from, to, role, ...extra context)` tuples and asserting an invariant
("a transition to `Reimbursed` is only reachable through a state history that
contains an `Approved` event") rather than trying to keep hand-maintaining an
exhaustive literal table.
