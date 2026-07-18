using ExpenseManagement.Api.Dtos;
using ExpenseManagement.Api.Security;
using ExpenseManagement.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly IUserContext _userContext;

    public AuthController(AuthService auth, IUserContext userContext)
    {
        _auth = auth;
        _userContext = userContext;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login(LoginRequest req)
    {
        var result = _auth.Login(req.Email, req.Password);
        if (result is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var (user, token) = result.Value;
        return Ok(new LoginResponse(token, user.ToResponse()));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        var raw = Request.Headers.Authorization.ToString();
        if (raw.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            _auth.Logout(raw["Bearer ".Length..].Trim());
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserResponse> Me() => Ok(_userContext.RequireUser().ToResponse());
}
