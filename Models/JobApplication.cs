namespace job_application_tracker_api.Models;

public class JobApplication
{
    public int Id { get; set; }

    // Null is reserved for preserved legacy records until a local operator assigns an owner.
    public string? UserId { get; set; }

    public string Company { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public DateOnly? DateApplied { get; set; }

    public string Status { get; set; } = "Ansökt";

    public string Notes { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }
}
