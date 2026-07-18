using System.ComponentModel.DataAnnotations;
using ExpenseManagement.Api.Domain;

namespace ExpenseManagement.Api.Dtos;

// ---- Auth ----

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record LoginResponse(string Token, UserResponse User);

public record UserResponse(Guid Id, string Email, string FullName, string Role, Guid? ManagerId);

// ---- Expenses ----

public record CreateExpenseRequest
{
    [Required, MaxLength(200)]
    public string Merchant { get; init; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Amount { get; init; }

    [Required, MaxLength(3), MinLength(3)]
    public string Currency { get; init; } = "USD";

    [Required]
    public ExpenseCategory Category { get; init; }

    [Required]
    public DateOnly ExpenseDate { get; init; }
}

// Same shape as create for this simple app; kept separate so they can diverge.
public record UpdateExpenseRequest
{
    [Required, MaxLength(200)]
    public string Merchant { get; init; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Range(0.01, 1_000_000)]
    public decimal Amount { get; init; }

    [Required, MaxLength(3), MinLength(3)]
    public string Currency { get; init; } = "USD";

    [Required]
    public ExpenseCategory Category { get; init; }

    [Required]
    public DateOnly ExpenseDate { get; init; }
}

public record RejectRequest([property: Required, MaxLength(500)] string Reason);

public record PolicyFlagResponse(string Code, string Message, string Severity);

public record ExpenseEventResponse(string From, string To, string ActorName, string? Note, DateTimeOffset At);

public record ReceiptResponse(
    Guid Id,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Status,
    decimal? ExtractedAmount,
    DateTimeOffset UploadedAt);

public record ExpenseResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    string Merchant,
    string Description,
    decimal Amount,
    string Currency,
    string Category,
    DateOnly ExpenseDate,
    string Status,
    string? RejectionReason,
    string? PaymentReference,
    ReceiptResponse? Receipt,
    IReadOnlyList<PolicyFlagResponse> PolicyFlags,
    IReadOnlyList<ExpenseEventResponse> History,
    IReadOnlyList<string> AvailableActions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// ---- Reporting ----

public record CategoryTotal(string Category, decimal Total, int Count);
public record StatusTotal(string Status, decimal Total, int Count);

public record ReportSummaryResponse(
    decimal TotalSubmitted,
    decimal TotalApproved,
    decimal TotalReimbursed,
    int PendingApprovalCount,
    IReadOnlyList<CategoryTotal> ByCategory,
    IReadOnlyList<StatusTotal> ByStatus);
