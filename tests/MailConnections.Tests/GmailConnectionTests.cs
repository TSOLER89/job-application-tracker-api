using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using job_application_tracker_api.Data;
using job_application_tracker_api.Email;
using job_application_tracker_api.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MailConnections.Tests;

public sealed class GmailConnectionTests
{
    [Fact]
    public async Task Unconfigured_provider_and_anonymous_access_do_not_create_connections()
    {
        await using var app = new MailFactory(configured: false);
        using var anonymous = app.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/email-connections")).StatusCode);
        using var owner = await app.Account();
        var list = await Json(await owner.GetAsync("/api/email-connections"));
        Assert.False(list.GetProperty("gmailConfigured").GetBoolean());
        Assert.Empty(list.GetProperty("accounts").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(owner, "/api/email-connections/gmail/connect")).StatusCode);
    }

    [Fact]
    public async Task Authorization_ticket_is_single_use_and_bound_to_the_original_browser_session()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        using var other = await app.Account();
        var ticket = (await Json(await Post(owner, "/api/email-connections/gmail/connect"))).GetProperty("authorizationUrl").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(ticket)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsync("/api/email-connections/gmail/connect", null)).StatusCode);
        var authorization = await owner.GetAsync(ticket);
        Assert.Equal(HttpStatusCode.Redirect, authorization.StatusCode);
        var query = QueryHelpers.ParseQuery(authorization.Headers.Location!.Query);
        Assert.Equal("https://accounts.google.com", authorization.Headers.Location.GetLeftPart(UriPartial.Authority));
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.Equal("http://localhost/signin-gmail", query["redirect_uri"].ToString());
        Assert.Contains(GmailConnectionService.ReadScope, query["scope"].ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(ticket)).StatusCode);
    }

    [Fact]
    public async Task Successful_callback_verifies_identity_encrypts_tokens_and_returns_only_owned_metadata()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        using var other = await app.Account();
        var flow = await app.Begin(owner);
        var callback = await owner.GetAsync(flow.Callback);
        Assert.Equal("http://localhost:5173/email-accounts?gmail=connected", callback.Headers.Location?.ToString());
        Assert.True(app.Google.PkceVerified);
        var body = await (await owner.GetAsync("/api/email-connections")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("fixture-access", body);
        Assert.DoesNotContain("fixture-refresh", body);
        var account = JsonDocument.Parse(body).RootElement.GetProperty("accounts")[0];
        Assert.Equal("fixture@example.test", account.GetProperty("email").GetString());
        var id = account.GetProperty("id").GetGuid();
        var stored = app.Read(database => database.EmailConnections.Single(value => value.Id == id).ProtectedTokens);
        Assert.DoesNotContain("fixture-access", stored);
        Assert.DoesNotContain("fixture-refresh", stored);
        Assert.Empty((await Json(await other.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(other, id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync("/api/email-connections/" + id)).StatusCode);
        Assert.Contains("gmail=failed", (await owner.GetAsync(flow.Callback)).Headers.Location!.ToString());
        var reconnect = await app.Begin(owner);
        await owner.GetAsync(reconnect.Callback);
        Assert.Single((await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
    }

    [Fact]
    public async Task Missing_correlation_tampered_state_and_logout_reject_callbacks()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        var flow = await app.Begin(owner);
        using var stranger = app.Client();
        Assert.Contains("gmail=failed", (await stranger.GetAsync(flow.Callback)).Headers.Location!.ToString());
        Assert.Contains("gmail=failed", (await owner.GetAsync("/signin-gmail?state=changed&code=fixture-code")).Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await Post(owner, "/api/auth/logout")).StatusCode);
        Assert.Contains("gmail=failed", (await owner.GetAsync(flow.Callback)).Headers.Location!.ToString());
        Assert.Equal(0, app.Google.ProfileReads);
    }

    [Fact]
    public async Task Switching_jobtrack_accounts_cancels_pending_google_authorization()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        using var other = await app.Account();
        var flow = await app.Begin(owner);
        var otherEmail = (await Json(await other.GetAsync("/api/auth/session"))).GetProperty("user").GetProperty("email").GetString();
        Assert.Equal(HttpStatusCode.OK, (await Post(owner, "/api/auth/login", new { email = otherEmail, password = "Verification12345" })).StatusCode);
        Assert.Contains("gmail=failed", (await owner.GetAsync(flow.Callback)).Headers.Location!.ToString());
        Assert.Equal(0, app.Google.ProfileReads);
        Assert.Empty((await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
    }

    [Fact]
    public async Task Missing_mail_permission_or_unverified_identity_does_not_connect()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        app.Google.IncludeMailPermission = false;
        var noPermission = await app.Begin(owner);
        Assert.Contains("gmail=failed", (await owner.GetAsync(noPermission.Callback)).Headers.Location!.ToString());
        app.Google.IncludeMailPermission = true;
        app.Google.VerifiedEmail = false;
        var unverified = await app.Begin(owner);
        Assert.Contains("gmail=failed", (await owner.GetAsync(unverified.Callback)).Headers.Location!.ToString());
        Assert.Empty((await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
    }

    [Fact]
    public async Task Expired_attempt_cannot_complete_and_missing_offline_access_cannot_create_a_connection()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        var user = (await Json(await owner.GetAsync("/api/auth/session"))).GetProperty("user").GetProperty("id").GetString();
        var expired = await app.Begin(owner);
        app.Read(database => database.EmailConnectionAttempts.Where(value => value.UserId == user)
            .ExecuteUpdate(update => update.SetProperty(value => value.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1))));
        Assert.Contains("gmail=failed", (await owner.GetAsync(expired.Callback)).Headers.Location!.ToString());
        app.Google.IncludeRefreshToken = false;
        var noOfflineAccess = await app.Begin(owner);
        Assert.Contains("gmail=failed", (await owner.GetAsync(noOfflineAccess.Callback)).Headers.Location!.ToString());
        Assert.Empty((await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
        app.Google.IncludeRefreshToken = true;
        var connected = await app.Begin(owner);
        await owner.GetAsync(connected.Callback);
        app.Google.IncludeRefreshToken = false;
        var reconnect = await app.Begin(owner);
        Assert.Contains("gmail=connected", (await owner.GetAsync(reconnect.Callback)).Headers.Location!.ToString());
        Assert.Single((await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
    }

    [Fact]
    public async Task Invalid_provider_revocation_request_is_not_reported_as_success()
    {
        await using var app = new MailFactory();
        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var tokens = JsonSerializer.Serialize(new { AccessToken = "fixture-access", RefreshToken = "fixture-refresh", ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        var connection = new EmailConnection
        {
            UserId = "fixture-user", ProviderAccountId = "fixture-subject",
            ProtectedTokens = provider.CreateProtector("JobTrack.GmailTokens.v1", "fixture-user", "fixture-subject").Protect(tokens)
        };
        using var client = new HttpClient(new InvalidRevocation());
        Assert.False(await scope.ServiceProvider.GetRequiredService<GmailConnectionService>().RevokeAsync(connection, client));
    }

    [Fact]
    public async Task Disconnect_revokes_google_and_invalidates_pending_callbacks_even_when_provider_is_unavailable()
    {
        await using var app = new MailFactory();
        using var owner = await app.Account();
        var flow = await app.Begin(owner);
        await owner.GetAsync(flow.Callback);
        var id = (await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts")[0].GetProperty("id").GetGuid();
        var pending = await app.Begin(owner);
        var removed = await Json(await Delete(owner, id));
        Assert.True(removed.GetProperty("providerRevoked").GetBoolean());
        Assert.Equal(1, app.Google.Revocations);
        Assert.Contains("gmail=failed", (await owner.GetAsync(pending.Callback)).Headers.Location!.ToString());
        var reconnect = await app.Begin(owner);
        await owner.GetAsync(reconnect.Callback);
        id = (await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts")[0].GetProperty("id").GetGuid();
        app.Google.FailRevocation = true;
        Assert.False((await Json(await Delete(owner, id))).GetProperty("providerRevoked").GetBoolean());
        Assert.Empty((await Json(await owner.GetAsync("/api/email-connections"))).GetProperty("accounts").EnumerateArray());
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        (await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync())).RootElement.Clone();

    internal static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body = null)
    {
        var csrf = (await Json(await client.GetAsync("/api/auth/session"))).GetProperty("csrfToken").GetString();
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> Delete(HttpClient client, Guid id)
    {
        var csrf = (await Json(await client.GetAsync("/api/auth/session"))).GetProperty("csrfToken").GetString();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/email-connections/" + id);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return await client.SendAsync(request);
    }
}

public sealed class InvalidRevocation : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { error = "invalid_request" }) });
}

public sealed class MailFactory : WebApplicationFactory<Program>
{
    private readonly bool configured;
    public MailFactory(bool configured = true)
    {
        this.configured = configured;
        // Minimal-host authentication registration reads these before the factory's configuration callback.
        // These values exist only inside the disposable testhost process; no real Google credentials are used.
        Environment.SetEnvironmentVariable("Email__Gmail__ClientId", configured ? "fixture-client" : " ");
        Environment.SetEnvironmentVariable("Email__Gmail__ClientSecret", configured ? "fixture-secret" : " ");
        Environment.SetEnvironmentVariable("Email__Gmail__ApiBaseUrl", "http://localhost");
        Environment.SetEnvironmentVariable("Email__Gmail__WebBaseUrl", "http://localhost:5173");
    }
    public FakeGoogle Google { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../..")));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\MSSQLLocalDB;Database=JobTrackMailVerification;Trusted_Connection=True;TrustServerCertificate=True;",
            ["Email:Gmail:ClientId"] = configured ? "fixture-client" : "",
            ["Email:Gmail:ClientSecret"] = configured ? "fixture-secret" : "",
            ["Email:Gmail:ApiBaseUrl"] = "http://localhost",
            ["Email:Gmail:WebBaseUrl"] = "http://localhost:5173",
            ["Logging:LogLevel:Default"] = "Warning",
            ["Logging:LogLevel:Microsoft.AspNetCore.Authentication"] = "Critical"
        }));
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<OAuthOptions>(GmailConnectionService.Scheme, options => options.Backchannel = new HttpClient(Google));
            services.AddHttpClient("GmailRevoke").ConfigurePrimaryHttpMessageHandler(() => Google);
        });
    }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (database.Database.GetDbConnection().Database != "JobTrackMailVerification")
            throw new InvalidOperationException("Only the dedicated mail test database is permitted.");
        database.Database.Migrate();
        return host;
    }
    public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
    public async Task<HttpClient> Account()
    {
        var client = Client();
        var response = await GmailConnectionTests.Post(client, "/api/auth/register",
            new { email = Guid.NewGuid().ToString("N") + "@example.test", password = "Verification12345" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return client;
    }
    public async Task<(string Callback, string State)> Begin(HttpClient client)
    {
        using var response = await GmailConnectionTests.Post(client, "/api/email-connections/gmail/connect");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var ticket = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authorizationUrl").GetString()!;
        var authorize = await client.GetAsync(ticket);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var query = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query);
        Google.ExpectedChallenge = query["code_challenge"].ToString();
        var state = query["state"].ToString();
        return ("/signin-gmail?state=" + Uri.EscapeDataString(state) + "&code=fixture-code", state);
    }
    public T Read<T>(Func<ApplicationDbContext, T> action)
    {
        using var scope = Services.CreateScope();
        return action(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }
}

public sealed class FakeGoogle : HttpMessageHandler
{
    public bool IncludeRefreshToken { get; set; } = true;
    public bool IncludeMailPermission { get; set; } = true;
    public bool VerifiedEmail { get; set; } = true;
    public bool FailRevocation { get; set; }
    public string ExpectedChallenge { get; set; } = "";
    public bool PkceVerified { get; private set; }
    public int ProfileReads { get; private set; }
    public int Revocations { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        object result;
        switch (request.RequestUri!.AbsoluteUri)
        {
            case "https://oauth2.googleapis.com/token":
                var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));
                var verifier = form["code_verifier"].ToString();
                PkceVerified = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))) == ExpectedChallenge;
                Assert.True(PkceVerified);
                result = new { access_token = "fixture-access", refresh_token = IncludeRefreshToken ? "fixture-refresh" : null, expires_in = 3600, token_type = "Bearer", scope = IncludeMailPermission ? GmailConnectionService.ReadScope : "openid email" };
                break;
            case "https://openidconnect.googleapis.com/v1/userinfo":
                result = new { sub = "fixture-google-subject", email = "fixture@example.test", email_verified = VerifiedEmail };
                break;
            case "https://gmail.googleapis.com/gmail/v1/users/me/profile":
                ProfileReads++;
                result = new { emailAddress = "fixture@example.test" };
                break;
            case "https://oauth2.googleapis.com/revoke":
                Revocations++;
                return new HttpResponseMessage(FailRevocation ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
            default:
                throw new InvalidOperationException("Unexpected outbound endpoint. No real network access is allowed.");
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
    }
}
