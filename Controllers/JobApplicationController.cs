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

    [HttpPost]
    public ActionResult<JobApplication> Create(JobApplication application)
    {
        application.Id = Applications.Count == 0
            ? 1
            : Applications.Max(a => a.Id) + 1;

        Applications.Add(application);

        return StatusCode(201, application);
    }

    [HttpPut("{id}")]
    public ActionResult<JobApplication> Update(int id, JobApplication updatedApplication)
    {
        var application = Applications.FirstOrDefault(a => a.Id == id);
        if (application == null)
        {
            return NotFound();
        }
        application.Company = updatedApplication.Company;
        application.Position = updatedApplication.Position;
        application.Location = updatedApplication.Location;
        application.DateApplied = updatedApplication.DateApplied;
        application.Status = updatedApplication.Status;
        application.Notes = updatedApplication.Notes;

        return Ok(application);

    }

    [HttpPost("upload")]
    public async Task<ActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Ingen fil valdes.");
        }
        
        var uploadsFolder = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "uploads"
        );

        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var imageUrl = $"/uploads/{fileName}";

        return Ok(new { imageUrl });
    }
}