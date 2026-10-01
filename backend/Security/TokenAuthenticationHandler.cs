using System.Security.Claims;
using System.Text.Encodings.Web;
using ExpenseManagement.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ExpenseManagement.Api.Security;

/// <summary>
/// Reads "Authorization: Bearer &lt;token&gt;", validates it via <see cref="AuthService"/>,
/// and builds a ClaimsPrincipal so that [Authorize] / [Authorize(Roles=...)] work
/// against our in-memory sessions. No third-party auth packages required.
/// </summary>
public class TokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Bearer";

    private readonly AuthService _auth;

    public TokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AuthService auth)
        : base(options, logger, encoder)
    {
        _auth = auth;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
            return Task.FromResult(AuthenticateResult.NoResult());

        string raw = header.ToString();
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        string token = raw["Bearer ".Length..].Trim();
        var user = _auth.ValidateToken(token);
        if (user is null)
            return Task.FromResult(AuthenticateResult.Fail("Invalid or expired token."));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
