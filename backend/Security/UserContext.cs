using System.Security.Claims;
using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Storage;

namespace ExpenseManagement.Api.Security;

/// <summary>Resolves the authenticated <see cref="User"/> for the current request.</summary>
public interface IUserContext
{
    Guid? UserId { get; }
    /// <summary>The acting user, or throws if the request is unauthenticated.</summary>
    User RequireUser();
}

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _accessor;
    private readonly DataStore _store;

    public UserContext(IHttpContextAccessor accessor, DataStore store)
    {
        _accessor = accessor;
        _store = store;
    }

    public Guid? UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public User RequireUser()
    {
        var id = UserId ?? throw new ForbiddenException("Not authenticated.");
        return _store.Users.GetValueOrDefault(id)
            ?? throw new ForbiddenException("Authenticated user no longer exists.");
    }
}
