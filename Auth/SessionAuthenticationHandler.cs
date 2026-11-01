using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using job_application_tracker_api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace job_application_tracker_api.Auth;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, ApplicationDbContext database)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "JobTrackSession";
    public const string CookieName = "JobTrack.Session";

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        var token = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authorization[7..].Trim()
            : string.IsNullOrEmpty(authorization) ? Request.Cookies[CookieName] : null;
        if (string.IsNullOrEmpty(token)) return AuthenticateResult.NoResult();
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return AuthenticateResult.Fail("Invalid session.");

        var hash = Hash(token);
        var session = await database.AuthSessions.AsNoTracking().Include(value => value.User)
            .SingleOrDefaultAsync(value => value.TokenHash == hash, Context.RequestAborted);
        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow ||
            session.SecurityStamp != session.User.SecurityStamp)
        {
            return AuthenticateResult.Fail("Session expired or revoked.");
        }

        var principal = CreatePrincipal(session.UserId, session.User.Email ?? string.Empty);
        Context.Items["SessionHash"] = hash;
        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    public static ClaimsPrincipal CreatePrincipal(string id, string email) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Email, email)], SchemeName));

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) =>
        Results.Problem(statusCode: 401, title: "Logga in för att fortsätta.").ExecuteAsync(Context);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        Results.Problem(statusCode: 403, title: "Du har inte behörighet till den här resursen.").ExecuteAsync(Context);
}
