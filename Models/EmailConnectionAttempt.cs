namespace job_application_tracker_api.Models;

public sealed class EmailConnectionAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TicketHash { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
