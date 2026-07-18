namespace ExpenseManagement.Api.Domain;

public class Receipt
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ExpenseId { get; set; }

    /// <summary>Server-generated name on disk. Never derived from user input (avoids path traversal).</summary>
    public required string StoredFileName { get; set; }

    /// <summary>Original client file name, kept for display only.</summary>
    public required string OriginalFileName { get; set; }

    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }

    public ReceiptStatus Status { get; set; } = ReceiptStatus.Pending;

    /// <summary>Amount produced by the (simulated) OCR step; used to cross-check the claimed amount.</summary>
    public decimal? ExtractedAmount { get; set; }

    public DateTimeOffset UploadedAt { get; init; } = DateTimeOffset.UtcNow;
}
