using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using job_application_tracker_api.Data;
using job_application_tracker_api.Models;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace job_application_tracker_api.Email;

public sealed class GmailConnectionService(ApplicationDbContext database, IDataProtectionProvider protection)
{
    public const string ReadScope = "https://www.googleapis.com/auth/gmail.readonly";
    public const string Scheme = "GmailConnection";

    public async Task SaveAsync(OAuthCreatingTicketContext context)
    {
        if (!context.Properties.Items.TryGetValue("attempt", out var attemptId) || !Guid.TryParse(attemptId, out var id))
            throw new InvalidOperationException("Invalid connection attempt.");
        var now = DateTimeOffset.UtcNow;
        var attempt = await database.EmailConnectionAttempts.SingleOrDefaultAsync(value => value.Id == id, context.HttpContext.RequestAborted);
        if (attempt is null || attempt.StartedAt is null || attempt.CompletedAt is not null || attempt.ExpiresAt <= now
            || !await ActiveSession(attempt))
            throw new InvalidOperationException("Connection attempt expired.");
        var granted = context.TokenResponse.Response?.RootElement.TryGetProperty("scope", out var scopes) == true
            ? scopes.GetString()?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [] : [];
        if (!granted.Contains(ReadScope) || string.IsNullOrEmpty(context.AccessToken))
            throw new InvalidOperationException("Gmail read permission was not granted.");

        // Both identities are fetched from fixed Google endpoints with this authorization's access token.
        using var identity = await GetGoogleJson(context.Backchannel, "https://openidconnect.googleapis.com/v1/userinfo", context.AccessToken, context.HttpContext.RequestAborted);
        using var profile = await GetGoogleJson(context.Backchannel, "https://gmail.googleapis.com/gmail/v1/users/me/profile", context.AccessToken, context.HttpContext.RequestAborted);
        var subject = identity.RootElement.GetProperty("sub").GetString();
        var email = profile.RootElement.GetProperty("emailAddress").GetString();
        var verifiedEmail = identity.RootElement.GetProperty("email").GetString();
        if (string.IsNullOrEmpty(subject) || subject.Length > 254 || string.IsNullOrEmpty(email) || email.Length > 254
            || !identity.RootElement.GetProperty("email_verified").GetBoolean()
            || !string.Equals(email, verifiedEmail, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Gmail identity could not be verified.");

        await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, context.HttpContext.RequestAborted);
        if (!await ActiveSession(attempt)) throw new InvalidOperationException("The initiating session ended.");
        var claimed = await database.EmailConnectionAttempts.Where(value => value.Id == id && value.CompletedAt == null && value.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.CompletedAt, now), context.HttpContext.RequestAborted);
        if (claimed != 1) throw new InvalidOperationException("Connection attempt already used.");
        var connection = await database.EmailConnections.SingleOrDefaultAsync(value =>
            value.UserId == attempt.UserId && value.Provider == "gmail" && value.ProviderAccountId == subject, context.HttpContext.RequestAborted);
        var protector = protection.CreateProtector("JobTrack.GmailTokens.v1", attempt.UserId, subject);
        var refresh = context.RefreshToken;
        if (string.IsNullOrEmpty(refresh) && connection is not null)
            refresh = JsonSerializer.Deserialize<SavedTokens>(protector.Unprotect(connection.ProtectedTokens))?.RefreshToken;
        if (string.IsNullOrEmpty(refresh)) throw new InvalidOperationException("Offline access was not granted.");
        if (connection is null)
        {
            connection = new EmailConnection { UserId = attempt.UserId, ProviderAccountId = subject, ConnectedAt = now };
            database.EmailConnections.Add(connection);
        }
        connection.Email = email;
        connection.VerifiedAt = now;
        connection.ProtectedTokens = protector.Protect(JsonSerializer.Serialize(
            new SavedTokens(context.AccessToken, refresh, now.Add(context.ExpiresIn ?? TimeSpan.FromHours(1)))));
        await database.SaveChangesAsync(context.HttpContext.RequestAborted);
        await transaction.CommitAsync(context.HttpContext.RequestAborted);
    }

    public async Task<bool> RevokeAsync(EmailConnection connection, HttpClient client)
    {
        try
        {
            var protector = protection.CreateProtector("JobTrack.GmailTokens.v1", connection.UserId, connection.ProviderAccountId);
            var saved = JsonSerializer.Deserialize<SavedTokens>(protector.Unprotect(connection.ProtectedTokens));
            if (saved is null || string.IsNullOrEmpty(saved.RefreshToken)) return false;
            using var response = await client.PostAsync("https://oauth2.googleapis.com/revoke",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = saved.RefreshToken }));
            if (response.IsSuccessStatusCode) return true;
            if (response.StatusCode != HttpStatusCode.BadRequest) return false;
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return problem.RootElement.TryGetProperty("error", out var code)
                && code.ValueKind == JsonValueKind.String && code.GetString() == "invalid_token";
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or System.Security.Cryptography.CryptographicException or JsonException)
        {
            return false;
        }
    }

    private Task<bool> ActiveSession(EmailConnectionAttempt attempt) => database.AuthSessions.AnyAsync(session =>
        session.TokenHash == attempt.SessionHash && session.UserId == attempt.UserId
        && session.ExpiresAt > DateTimeOffset.UtcNow && session.SecurityStamp == session.User.SecurityStamp);

    private static async Task<JsonDocument> GetGoogleJson(HttpClient client, string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    private sealed record SavedTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);
}
