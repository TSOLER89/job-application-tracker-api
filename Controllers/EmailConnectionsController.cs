using System.Security.Claims;
using System.Security.Cryptography;
using job_application_tracker_api.Auth;
using job_application_tracker_api.Data;
using job_application_tracker_api.Email;
using job_application_tracker_api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace job_application_tracker_api.Controllers;

[ApiController]
[Authorize]
[Route("api/email-connections")]
public sealed class EmailConnectionsController(ApplicationDbContext database, GmailConnectionOptions settings,
    GmailConnectionService gmail, IHttpClientFactory clients) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string SessionHash => HttpContext.Items["SessionHash"] as string ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var accounts = await database.EmailConnections.AsNoTracking().Where(value => value.UserId == UserId)
            .OrderBy(value => value.Email)
            .Select(value => new { value.Id, value.Provider, value.Email, value.ConnectedAt, value.VerifiedAt })
            .ToListAsync(cancellationToken);
        return Ok(new { gmailConfigured = settings.IsConfigured, connectOnWeb = true, accounts });
    }

    [HttpPost("gmail/connect")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Connect(CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
            return Problem(statusCode: 409, title: "Gmail behöver konfigureras på servern innan ett konto kan anslutas.");
        if (!settings.MatchesApi(Request))
            return Problem(statusCode: 409, title: "Öppna webbappen på den dator och adress som används i Gmail-konfigurationen.");
        if (Request.Headers.ContainsKey("Authorization") || !Request.Cookies.ContainsKey(SessionAuthenticationHandler.CookieName))
            return Problem(statusCode: 409, title: "Anslut Gmail från webbappen på datorn. Kopplingen visas sedan även i mobilen.");
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await database.EmailConnectionAttempts.Where(value => value.ExpiresAt <= DateTimeOffset.UtcNow).ExecuteDeleteAsync(cancellationToken);
        database.EmailConnectionAttempts.Add(new EmailConnectionAttempt
        {
            UserId = UserId, SessionHash = SessionHash, TicketHash = SessionAuthenticationHandler.Hash(ticket),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10)
        });
        await database.SaveChangesAsync(cancellationToken);
        GmailAuthentication.NoStore(Response);
        return Ok(new { authorizationUrl = settings.ApiBaseUrl.TrimEnd('/') + "/api/email-connections/gmail/authorize?ticket=" + ticket });
    }

    [HttpGet("gmail/authorize")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> AuthorizeGmail([FromQuery] string ticket, CancellationToken cancellationToken)
    {
        GmailAuthentication.NoStore(Response);
        if (!settings.IsConfigured || !settings.MatchesApi(Request) || Request.Headers.ContainsKey("Authorization")
            || !Request.Cookies.ContainsKey(SessionAuthenticationHandler.CookieName)
            || ticket.Length != 64 || !ticket.All(Uri.IsHexDigit))
            return NotFound();
        var hash = SessionAuthenticationHandler.Hash(ticket);
        var now = DateTimeOffset.UtcNow;
        var attempt = await database.EmailConnectionAttempts.SingleOrDefaultAsync(value => value.TicketHash == hash
            && value.UserId == UserId && value.SessionHash == SessionHash && value.ExpiresAt > now, cancellationToken);
        if (attempt is null) return NotFound();
        var claimed = await database.EmailConnectionAttempts.Where(value => value.Id == attempt.Id && value.StartedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.StartedAt, now), cancellationToken);
        if (claimed != 1) return NotFound();
        var properties = new AuthenticationProperties();
        properties.Items["attempt"] = attempt.Id.ToString();
        return Challenge(properties, GmailConnectionService.Scheme);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken cancellationToken)
    {
        var connection = await database.EmailConnections.SingleOrDefaultAsync(value => value.Id == id && value.UserId == UserId, cancellationToken);
        if (connection is null) return NotFound();
        // Invalidate pending authorizations too, so an old callback cannot restore a removed connection.
        await database.EmailConnectionAttempts.Where(value => value.UserId == UserId && value.CompletedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.CompletedAt, DateTimeOffset.UtcNow), cancellationToken);
        var providerRevoked = await gmail.RevokeAsync(connection, clients.CreateClient("GmailRevoke"));
        database.EmailConnections.Remove(connection);
        await database.SaveChangesAsync(CancellationToken.None);
        return Ok(new { disconnected = true, providerRevoked });
    }
}
