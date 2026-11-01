namespace job_application_tracker_api.Models;

public sealed class EmailConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Provider { get; set; } = "gmail";
    public string ProviderAccountId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ProtectedTokens { get; set; } = string.Empty;
    public DateTimeOffset ConnectedAt { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
}
