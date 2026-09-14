using job_application_tracker_api.Models;
using Microsoft.AspNetCore.Mvc;

namespace job_application_tracker_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobApplicationsController : ControllerBase
{
    private static readonly List<JobApplication> Applications =
    [
        new JobApplication
        {
            Id = 1,
            Company = "Consid",
            Position = "Junior .NET-utvecklare",
            Location = "Linköping",
            DateApplied = new DateOnly(2026, 9, 1),
            Status = "Intervju",
            Notes = ""
        },

        new JobApplication
        {
            Id = 2,
            Company = "Sectra",
            Position = "Systemutvecklare",
            Location = "Linköping",
            DateApplied = new DateOnly(2026, 9, 9),
            Status = "Ansökt",
            Notes = ""
        },

        new JobApplication
        {
            Id = 3,
            Company = "Saab",
            Position = "Junior Software Developer",
            Location = "Linköping",
            Status = "Intresserad",
            Notes = ""
        }
    ];

    [HttpGet]
    public ActionResult<List<JobApplication>> GetAll()
    {
        return Ok(Applications);
    }
}