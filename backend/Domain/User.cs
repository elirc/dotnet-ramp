namespace ExpenseManagement.Api.Domain;

public class User
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string FullName { get; set; }

    // Stored separately so we never persist or transmit plaintext passwords.
    public required string PasswordHash { get; set; }
    public required string PasswordSalt { get; set; }

    public UserRole Role { get; set; }

    /// <summary>The manager who approves this user's expenses (null for managers/finance).</summary>
    public Guid? ManagerId { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
