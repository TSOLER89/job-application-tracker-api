namespace job_application_tracker_api.Models;

public sealed class UploadedImage
{
    public string FileName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
