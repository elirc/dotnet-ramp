using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Security;

namespace ExpenseManagement.Api.Storage;

/// <summary>
/// Seeds demo users and a couple of sample expenses so the app is usable on first run.
/// Passwords are hashed exactly like real ones — no plaintext is stored.
/// </summary>
public static class DataSeeder
{
    public const string DemoPassword = "Password123!";

    public static void Seed(DataStore store)
    {
        if (!store.Users.IsEmpty) return;

        var manager = NewUser("manager@demo.com", "Morgan Manager", UserRole.Manager, managerId: null);
        var finance = NewUser("finance@demo.com", "Fran Finance", UserRole.Finance, managerId: null);
        var employee = NewUser("employee@demo.com", "Eli Employee", UserRole.Employee, managerId: manager.Id);
        var employee2 = NewUser("dana@demo.com", "Dana Dev", UserRole.Employee, managerId: manager.Id);

        foreach (var u in new[] { manager, finance, employee, employee2 })
            store.Users[u.Id] = u;

        // A draft and a submitted expense for the demo employee.
        var draft = new Expense
        {
            EmployeeId = employee.Id,
            Merchant = "Blue Bottle Coffee",
            Description = "Client meeting coffee",
            Amount = 18.50m,
            Category = ExpenseCategory.Meals,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Status = ExpenseStatus.Draft
        };

        var submitted = new Expense
        {
            EmployeeId = employee.Id,
            Merchant = "United Airlines",
            Description = "Flight to customer onsite",
            Amount = 420.00m,
            Category = ExpenseCategory.Travel,
            ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            Status = ExpenseStatus.Submitted
        };
        submitted.History.Add(new ExpenseEvent(
            ExpenseStatus.Draft, ExpenseStatus.Submitted,
            employee.Id, employee.FullName, null, DateTimeOffset.UtcNow.AddDays(-6)));

        store.Expenses[draft.Id] = draft;
        store.Expenses[submitted.Id] = submitted;
    }

    private static User NewUser(string email, string name, UserRole role, Guid? managerId)
    {
        var (hash, salt) = PasswordHasher.Hash(DemoPassword);
        return new User
        {
            Email = email,
            FullName = name,
            Role = role,
            ManagerId = managerId,
            PasswordHash = hash,
            PasswordSalt = salt
        };
    }
}
