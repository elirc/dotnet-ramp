using ExpenseManagement.Api.Domain;

namespace ExpenseManagement.Api.Services;

/// <summary>
/// Evaluates an expense against company spend policy. Rules return either a
/// Warning (recorded for the approver) or a Blocking flag (prevents submission).
/// Keeping every rule in one place makes the policy easy to read and extend.
/// </summary>
public class PolicyEngine
{
    private const decimal ReceiptRequiredThreshold = 25m;
    private const decimal HighValueThreshold = 1_000m;
    private const int StaleDays = 90;

    // Per-category soft limits (per single expense). Exceeding them is a warning,
    // signalling the approver to scrutinise — not an automatic block.
    private static readonly IReadOnlyDictionary<ExpenseCategory, decimal> CategoryLimits =
        new Dictionary<ExpenseCategory, decimal>
        {
            [ExpenseCategory.Meals] = 75m,
            [ExpenseCategory.Travel] = 2_000m,
            [ExpenseCategory.Lodging] = 400m,
            [ExpenseCategory.Transportation] = 200m,
            [ExpenseCategory.OfficeSupplies] = 300m,
            [ExpenseCategory.Software] = 1_000m,
            [ExpenseCategory.Entertainment] = 150m,
            [ExpenseCategory.Other] = 500m
        };

    /// <param name="hasProcessedReceipt">
    /// True when a receipt is attached and has cleared background processing.
    /// </param>
    public IReadOnlyList<PolicyFlag> Evaluate(Expense expense, bool hasProcessedReceipt)
    {
        var flags = new List<PolicyFlag>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (expense.Amount <= 0)
            flags.Add(new PolicyFlag("AMOUNT_INVALID", "Amount must be greater than zero.", PolicySeverity.Blocking));

        if (string.IsNullOrWhiteSpace(expense.Description))
            flags.Add(new PolicyFlag("DESCRIPTION_REQUIRED", "A description is required.", PolicySeverity.Blocking));

        if (expense.ExpenseDate > today)
            flags.Add(new PolicyFlag("DATE_FUTURE", "Expense date cannot be in the future.", PolicySeverity.Blocking));
        else if (expense.ExpenseDate < today.AddDays(-StaleDays))
            flags.Add(new PolicyFlag("DATE_STALE", $"Expense is older than {StaleDays} days.", PolicySeverity.Warning));

        if (expense.Amount > ReceiptRequiredThreshold && expense.ReceiptId is null)
            flags.Add(new PolicyFlag("RECEIPT_REQUIRED",
                $"A receipt is required for amounts over {ReceiptRequiredThreshold:C}.", PolicySeverity.Blocking));
        else if (expense.ReceiptId is not null && !hasProcessedReceipt)
            flags.Add(new PolicyFlag("RECEIPT_PROCESSING",
                "Receipt is still being processed.", PolicySeverity.Warning));

        if (CategoryLimits.TryGetValue(expense.Category, out var limit) && expense.Amount > limit)
            flags.Add(new PolicyFlag("OVER_CATEGORY_LIMIT",
                $"Amount exceeds the {expense.Category} limit of {limit:C}.", PolicySeverity.Warning));

        if (expense.Amount > HighValueThreshold)
            flags.Add(new PolicyFlag("HIGH_VALUE",
                $"High-value expense (> {HighValueThreshold:C}); requires extra scrutiny.", PolicySeverity.Warning));

        return flags;
    }

    public static bool HasBlocking(IEnumerable<PolicyFlag> flags) =>
        flags.Any(f => f.Severity == PolicySeverity.Blocking);
}
