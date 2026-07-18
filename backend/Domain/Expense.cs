namespace ExpenseManagement.Api.Domain;

/// <summary>A single state change, kept as an immutable audit trail entry.</summary>
public record ExpenseEvent(
    ExpenseStatus From,
    ExpenseStatus To,
    Guid ActorId,
    string ActorName,
    string? Note,
    DateTimeOffset At);

/// <summary>Result of one policy rule against an expense.</summary>
public record PolicyFlag(string Code, string Message, PolicySeverity Severity);

public class Expense
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid EmployeeId { get; set; }

    public string Merchant { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public ExpenseCategory Category { get; set; }
    public DateOnly ExpenseDate { get; set; }

    public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;

    public Guid? ReceiptId { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Set once reimbursement has been processed by the background worker.</summary>
    public string? PaymentReference { get; set; }

    /// <summary>Latest policy evaluation, refreshed on save/submit.</summary>
    public List<PolicyFlag> PolicyFlags { get; set; } = new();

    /// <summary>Append-only audit trail of every state transition.</summary>
    public List<ExpenseEvent> History { get; set; } = new();

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsEditable => Status is ExpenseStatus.Draft or ExpenseStatus.Rejected;
}
