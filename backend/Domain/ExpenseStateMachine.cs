namespace ExpenseManagement.Api.Domain;

/// <summary>
/// Single source of truth for the expense lifecycle. Every status change must go
/// through here so that illegal transitions are impossible regardless of which
/// controller/service triggers them.
///
///   Draft ──submit──▶ Submitted ──approve──▶ Approved ──reimburse──▶ Reimbursing ──(system)──▶ Reimbursed
///     ▲                   │
///     │                   └──reject──▶ Rejected ──submit──▶ Submitted
///     └─────────(edit while Draft/Rejected)
/// </summary>
public static class ExpenseStateMachine
{
    /// <summary>Sentinel actor id used by the background worker for system transitions.</summary>
    public static readonly Guid SystemActorId = Guid.Empty;

    // from -> (to -> roles allowed to trigger it). Empty role array == system-only.
    private static readonly IReadOnlyDictionary<ExpenseStatus, IReadOnlyDictionary<ExpenseStatus, UserRole[]>> Transitions =
        new Dictionary<ExpenseStatus, IReadOnlyDictionary<ExpenseStatus, UserRole[]>>
        {
            [ExpenseStatus.Draft] = new Dictionary<ExpenseStatus, UserRole[]>
            {
                [ExpenseStatus.Submitted] = new[] { UserRole.Employee }
            },
            [ExpenseStatus.Rejected] = new Dictionary<ExpenseStatus, UserRole[]>
            {
                [ExpenseStatus.Submitted] = new[] { UserRole.Employee }
            },
            [ExpenseStatus.Submitted] = new Dictionary<ExpenseStatus, UserRole[]>
            {
                [ExpenseStatus.Approved] = new[] { UserRole.Manager },
                [ExpenseStatus.Rejected] = new[] { UserRole.Manager }
            },
            [ExpenseStatus.Approved] = new Dictionary<ExpenseStatus, UserRole[]>
            {
                [ExpenseStatus.Reimbursing] = new[] { UserRole.Finance }
            },
            [ExpenseStatus.Reimbursing] = new Dictionary<ExpenseStatus, UserRole[]>
            {
                [ExpenseStatus.Reimbursed] = Array.Empty<UserRole>() // system-only
            }
        };

    public static bool IsTerminal(ExpenseStatus status) => status is ExpenseStatus.Reimbursed;

    public static IReadOnlyCollection<ExpenseStatus> NextStates(ExpenseStatus from) =>
        Transitions.TryGetValue(from, out var map) ? map.Keys.ToArray() : Array.Empty<ExpenseStatus>();

    /// <summary>True if <paramref name="role"/> is permitted to move from -> to.</summary>
    public static bool CanTransition(ExpenseStatus from, ExpenseStatus to, UserRole role) =>
        Transitions.TryGetValue(from, out var map)
        && map.TryGetValue(to, out var roles)
        && roles.Contains(role);

    /// <summary>Only edges explicitly marked system-only may be triggered by a worker.</summary>
    public static bool CanSystemTransition(ExpenseStatus from, ExpenseStatus to) =>
        Transitions.TryGetValue(from, out var map)
        && map.TryGetValue(to, out var roles)
        && roles.Length == 0;
}
