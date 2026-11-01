using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;

namespace job_application_tracker_api.Email;

public static class GmailAuthentication
{
    public const string CorrelationPrefix = "JobTrack.GmailCorrelation.";
    public static void AddGmailConnections(this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var settings = configuration.GetSection("Email:Gmail").Get<GmailConnectionOptions>() ?? new();
        services.AddSingleton(settings);
        services.AddScoped<GmailConnectionService>();
        services.AddHttpClient("GmailRevoke", client => client.Timeout = TimeSpan.FromSeconds(15));
        if (!settings.IsConfigured) return;
        services.AddAuthentication().AddCookie("EmailExternal", options =>
        {
            options.Cookie.Name = "JobTrack.UnusedExternal";
            options.Cookie.HttpOnly = true;
        }).AddOAuth(GmailConnectionService.Scheme, options =>
        {
            options.SignInScheme = "EmailExternal";
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            options.TokenEndpoint = "https://oauth2.googleapis.com/token";
            options.CallbackPath = "/signin-gmail";
            options.UsePkce = true;
            options.SaveTokens = false;
            options.RemoteAuthenticationTimeout = TimeSpan.FromMinutes(10);
            options.BackchannelTimeout = TimeSpan.FromSeconds(15);
            options.CorrelationCookie.HttpOnly = true;
            options.CorrelationCookie.Name = CorrelationPrefix;
            options.CorrelationCookie.Path = "/";
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("email");
            options.Scope.Add(GmailConnectionService.ReadScope);
            options.Events = new OAuthEvents
            {
                OnRedirectToAuthorizationEndpoint = context =>
                {
                    context.Response.Redirect(QueryHelpers.AddQueryString(context.RedirectUri,
                        new Dictionary<string, string?> { ["access_type"] = "offline", ["prompt"] = "consent select_account" }));
                    return Task.CompletedTask;
                },
                OnCreatingTicket = context => context.HttpContext.RequestServices.GetRequiredService<GmailConnectionService>().SaveAsync(context),
                OnTicketReceived = context =>
                {
                    // No external sign-in cookie or provider token is issued to the frontend.
                    context.HandleResponse();
                    NoStore(context.Response);
                    context.Response.Redirect(settings.ResultUrl("connected"));
                    return Task.CompletedTask;
                },
                OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    NoStore(context.Response);
                    context.Response.Redirect(settings.ResultUrl("failed"));
                    return Task.CompletedTask;
                }
            };
        });
    }

    public static void NoStore(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers["Referrer-Policy"] = "no-referrer";
    }

    public static void CancelPendingBrowserConnections(HttpContext context)
    {
        foreach (var name in context.Request.Cookies.Keys.Where(name => name.StartsWith(CorrelationPrefix, StringComparison.Ordinal)))
            context.Response.Cookies.Delete(name, new CookieOptions { Path = "/", HttpOnly = true, SameSite = SameSiteMode.Lax, Secure = context.Request.IsHttps });
    }
}
