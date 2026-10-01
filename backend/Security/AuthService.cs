using System.Security.Cryptography;
using ExpenseManagement.Api.Domain;
using ExpenseManagement.Api.Storage;

namespace ExpenseManagement.Api.Security;

/// <summary>
/// Issues and validates opaque bearer tokens backed by in-memory sessions.
/// (A production system would use signed JWTs or a session store; the surface
/// area here is deliberately the same so it is easy to swap.)
/// </summary>
public class AuthService
{
    private readonly DataStore _store;
    private readonly TimeSpan _sessionLifetime;

    public AuthService(DataStore store, IConfiguration config)
    {
        _store = store;
        int hours = config.GetValue("Auth:SessionLifetimeHours", 8);
        _sessionLifetime = TimeSpan.FromHours(hours);
    }

    public (User user, string token)? Login(string email, string password)
    {
        var user = _store.FindUserByEmail(email);
        // Verify even when the user is missing? We still short-circuit, but the
        // password check itself is constant-time to avoid trivial timing leaks.
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return null;

        string token = GenerateToken();
        var session = new Session(token, user.Id, DateTimeOffset.UtcNow.Add(_sessionLifetime));
        _store.Sessions[token] = session;
        return (user, token);
    }

    public void Logout(string token) => _store.Sessions.TryRemove(token, out _);

    /// <summary>Resolves a token to its user, pruning the session if it has expired.</summary>
    public User? ValidateToken(string token)
    {
        if (!_store.Sessions.TryGetValue(token, out var session))
            return null;

        if (session.IsExpired)
        {
            _store.Sessions.TryRemove(token, out _);
            return null;
        }

        return _store.Users.GetValueOrDefault(session.UserId);
    }

    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
}
