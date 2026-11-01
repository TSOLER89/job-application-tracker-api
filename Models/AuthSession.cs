using Microsoft.AspNetCore.Identity;

namespace job_application_tracker_api.Models;

public sealed class AuthSession
{
    public string TokenHash { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public IdentityUser User { get; set; } = null!;
    public string SecurityStamp { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
