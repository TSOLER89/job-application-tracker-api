using job_application_tracker_api.Models;

namespace job_application_tracker_api.Contracts;

public sealed record JobApplicationResponse(
    int Id, string Company, string Position, string Location,
    DateOnly? DateApplied, string Status, string Notes, string? ImageUrl,
    DateOnly? ApplicationDeadline, DateOnly? FollowUpDate)
{
    public static JobApplicationResponse FromEntity(JobApplication application) => new(
        application.Id, application.Company, application.Position, application.Location,
        application.DateApplied, application.Status, application.Notes, application.ImageUrl,
        application.ApplicationDeadline, application.FollowUpDate);
}
