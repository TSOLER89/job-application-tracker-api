namespace job_application_tracker_api.Email;

public sealed class GmailConnectionOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "http://localhost:5250";
    public string WebBaseUrl { get; set; } = "http://localhost:5173";
    public string CallbackUrl => ApiBaseUrl.TrimEnd('/') + "/signin-gmail";
    public string ResultUrl(string result) => WebBaseUrl.TrimEnd('/') + "/email-accounts?gmail=" + result;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret)
        && ValidOrigin(ApiBaseUrl) && ValidOrigin(WebBaseUrl);

    public bool MatchesApi(HttpRequest request) =>
        string.Equals(request.Scheme + "://" + request.Host, ApiBaseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
        && request.PathBase == PathString.Empty;

    private static bool ValidOrigin(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo)
        && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback))
        && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0;
}
