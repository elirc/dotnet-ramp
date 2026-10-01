using System.Collections.Concurrent;
using ExpenseManagement.Api.Domain;

namespace ExpenseManagement.Api.Storage;

/// <summary>
/// In-memory data store (registered as a singleton). Services currently read
/// the dictionaries directly, so a durable database needs an explicit refactor.
///
/// ConcurrentDictionary makes individual lookups and replacements thread-safe.
/// Multi-field expense commands use the per-expense gates below because a
/// dictionary does not make a sequence of entity mutations atomic.
/// </summary>
public class DataStore
{
    public ConcurrentDictionary<Guid, User> Users { get; } = new();
    public ConcurrentDictionary<Guid, Expense> Expenses { get; } = new();
    public ConcurrentDictionary<Guid, Receipt> Receipts { get; } = new();

    /// <summary>Active auth sessions keyed by opaque bearer token.</summary>
    public ConcurrentDictionary<string, Session> Sessions { get; } = new();

    private readonly ConcurrentDictionary<Guid, object> _expenseLocks = new();

    /// <summary>Shared gate for commands and workers that mutate one expense.</summary>
    public object GetExpenseLock(Guid expenseId) => _expenseLocks.GetOrAdd(expenseId, _ => new object());

    public User? FindUserByEmail(string email) =>
        Users.Values.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<User> DirectReportsOf(Guid managerId) =>
        Users.Values.Where(u => u.ManagerId == managerId);
}

public record Session(string Token, Guid UserId, DateTimeOffset ExpiresAt)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
