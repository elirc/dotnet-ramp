namespace ExpenseManagement.Api.Domain;

/// <summary>Base for expected, user-facing errors. Mapped to HTTP status codes by middleware.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

/// <summary>A requested resource does not exist -> 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Caller is authenticated but not allowed to act on this resource -> 403.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>A business rule was violated (bad state transition, policy block, etc.) -> 422.</summary>
public sealed class DomainException : AppException
{
    public IReadOnlyList<string> Details { get; }

    public DomainException(string message, IReadOnlyList<string>? details = null) : base(message)
    {
        Details = details ?? Array.Empty<string>();
    }
}
