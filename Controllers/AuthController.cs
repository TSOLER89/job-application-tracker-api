using System.Security.Claims;
using System.Security.Cryptography;
using job_application_tracker_api.Auth;
using job_application_tracker_api.Contracts;
using job_application_tracker_api.Data;
using job_application_tracker_api.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace job_application_tracker_api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<IdentityUser> users, ApplicationDbContext database,
    IAntiforgery antiforgery, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("session")]
    [AllowAnonymous]
    public IActionResult Session([FromHeader(Name = "X-Auth-Mode")] string? authMode = null) => SessionResponse(bearerMode: authMode == "bearer");

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterRequest request, [FromHeader(Name = "X-Auth-Mode")] string? authMode = null)
    {
        var email = request.Email.Trim();
        var user = new IdentityUser { UserName = email, Email = email, LockoutEnabled = true };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["account"] = result.Errors.Select(error => error.Description).Distinct().ToArray()
            }));
        }
        return await StartSession(user, StatusCodes.Status201Created, authMode == "bearer");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginRequest request, [FromHeader(Name = "X-Auth-Mode")] string? authMode = null)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || await users.IsLockedOutAsync(user)) return InvalidLogin();
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return InvalidLogin();
        }
        await users.ResetAccessFailedCountAsync(user);
        return await StartSession(user, StatusCodes.Status200OK, authMode == "bearer");
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (HttpContext.Items["SessionHash"] is string hash)
        {
            await database.AuthSessions.Where(session => session.TokenHash == hash)
                .ExecuteDeleteAsync(cancellationToken);
        }
        Response.Cookies.Delete(SessionAuthenticationHandler.CookieName, CookieSettings());
        return NoContent();
    }

    private ObjectResult InvalidLogin() => Problem(statusCode: 401,
        title: "Fel mejladress eller lösenord. Vid flera misslyckade försök, vänta 15 minuter och försök igen.");

    private async Task<IActionResult> StartSession(IdentityUser user, int statusCode, bool bearerMode)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        database.AuthSessions.Add(new AuthSession
        {
            TokenHash = SessionAuthenticationHandler.Hash(token), UserId = user.Id,
            SecurityStamp = await users.GetSecurityStampAsync(user), ExpiresAt = expiresAt
        });
        // Remove expired sessions without touching other active logins.
        await database.AuthSessions.Where(session => session.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(HttpContext.RequestAborted);
        await database.SaveChangesAsync(HttpContext.RequestAborted);
        HttpContext.User = SessionAuthenticationHandler.CreatePrincipal(user.Id, user.Email!);

        if (!bearerMode)
        {
            var settings = CookieSettings();
            settings.Expires = expiresAt;
            Response.Cookies.Append(SessionAuthenticationHandler.CookieName, token, settings);
        }
        Response.StatusCode = statusCode;
        return SessionResponse(bearerMode ? token : null, expiresAt, bearerMode);
    }

    private IActionResult SessionResponse(string? accessToken = null, DateTimeOffset? expiresAt = null, bool bearerMode = false)
    {
        Response.Headers.CacheControl = "no-store";
        var authenticated = User.Identity?.IsAuthenticated == true;
        var user = authenticated
            ? new AccountResponse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.FindFirstValue(ClaimTypes.Email)!)
            : null;
        var csrfToken = bearerMode
            ? null : antiforgery.GetAndStoreTokens(HttpContext).RequestToken;
        return new ObjectResult(new { user, csrfToken, accessToken, expiresAt }) { StatusCode = Response.StatusCode };
    }

    private CookieOptions CookieSettings() => new()
    {
        HttpOnly = true, Secure = !environment.IsDevelopment() || Request.IsHttps,
        SameSite = SameSiteMode.Strict, Path = "/", IsEssential = true
    };
}
