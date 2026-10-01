using System.Collections.Concurrent;
using ExpenseManagement.Api.Domain;

namespace ExpenseManagement.Api.Storage;

/// <summary>
/// In-memory data store (registered as a singleton). Swapping this for a real
/// database later is the only persistence change required — services depend on
/// the simple accessor methods below, not on the dictionaries directly.
///
/// ConcurrentDictionary makes individual reads/writes thread-safe, which matters
/// because the background worker mutates entities on a separate thread.
/// </summary>
public class DataStore
{
    public ConcurrentDictionary<Guid, User> Users { get; } = new();
    public ConcurrentDictionary<Guid, Expense> Expenses { get; } = new();
    public ConcurrentDictionary<Guid, Receipt> Receipts { get; } = new();

    /// <summary>Active auth sessions keyed by opaque bearer token.</summary>
    public ConcurrentDictionary<string, Session> Sessions { get; } = new();

    public User? FindUserByEmail(string email) =>
        Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<User> DirectReportsOf(Guid managerId) =>
        Users.Values.Where(u => u.ManagerId == managerId);
}

public record Session(string Token, Guid UserId, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
