using ExpenseManagement.Api.Domain;

namespace ExpenseManagement.Api.Services;

/// <summary>
/// Validates and persists receipt files to local disk. Security choices:
///  - allowlist of content types + extensions (never trust the client blindly),
///  - hard size cap,
///  - the on-disk name is a server-generated GUID, so a malicious original file
///    name can never escape the upload directory (no path traversal).
/// </summary>
public class ReceiptStorage
{
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["application/pdf"] = ".pdf"
        };

    private readonly string _root;
    private readonly long _maxSizeBytes;

    public ReceiptStorage(IConfiguration config, IWebHostEnvironment env)
    {
        var configured = config.GetValue<string>("Storage:ReceiptUploadPath") ?? "uploads";
        // Store OUTSIDE wwwroot so receipts are only served through the authorized endpoint.
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_root);

        _maxSizeBytes = config.GetValue("Storage:MaxReceiptSizeBytes", 5L * 1024 * 1024);
    }

    public async Task<Receipt> SaveAsync(Guid expenseId, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            throw new DomainException("Uploaded file is empty.");

        if (file.Length > _maxSizeBytes)
            throw new DomainException($"File exceeds the maximum size of {_maxSizeBytes / (1024 * 1024)} MB.");

        if (!AllowedTypes.TryGetValue(file.ContentType, out var extension))
            throw new DomainException("Unsupported file type. Allowed: JPEG, PNG, PDF.");

        // Cross-check the client-supplied extension against the content type.
        var originalExt = Path.GetExtension(file.FileName);
        if (!string.Equals(NormalizeExt(originalExt), extension, StringComparison.OrdinalIgnoreCase)
            && !(extension == ".jpg" && NormalizeExt(originalExt) == ".jpeg"))
        {
            throw new DomainException("File extension does not match its content type.");
        }

        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_root, storedName);

        await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, ct);
        }

        return new Receipt
        {
            ExpenseId = expenseId,
            StoredFileName = storedName,
            OriginalFileName = Path.GetFileName(file.FileName), // strip any path components
            ContentType = file.ContentType,
            SizeBytes = file.Length
        };
    }

    /// <summary>Opens a stored receipt for reading. Guards against path traversal.</summary>
    public (Stream stream, string contentType) OpenRead(Receipt receipt)
    {
        // StoredFileName is server-generated, but validate defensively anyway.
        var fullPath = Path.GetFullPath(Path.Combine(_root, receipt.StoredFileName));
        if (!fullPath.StartsWith(Path.GetFullPath(_root), StringComparison.Ordinal) || !File.Exists(fullPath))
            throw new NotFoundException("Receipt file not found.");

        return (File.OpenRead(fullPath), receipt.ContentType);
    }

    private static string NormalizeExt(string ext) =>
        string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant();
}
