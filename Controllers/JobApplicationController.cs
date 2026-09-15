using job_application_tracker_api.Data;
using job_application_tracker_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace job_application_tracker_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobApplicationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public JobApplicationsController(ApplicationDbContext context)
    {
        _context = context;
    }


    [HttpGet]
    public async Task<ActionResult<List<JobApplication>>> GetAll()
    {
        var applications = await _context.JobApplications
            .ToListAsync();

        return Ok(applications);
    }

    [HttpPost]
    public async Task<ActionResult<JobApplication>> Create(JobApplication application)
    {
        _context.JobApplications.Add(application);
        await _context.SaveChangesAsync();

        return StatusCode(201, application);
    }


    [HttpPut("{id}")]
    public async Task<ActionResult<JobApplication>> Update(
        int id,
        JobApplication updatedApplication
    )
    {
        var application = await _context.JobApplications
            .FirstOrDefaultAsync(a => a.Id == id);

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
        application.ImageUrl = updatedApplication.ImageUrl;

        await _context.SaveChangesAsync();

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

        var fileName =
            $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        var filePath = Path.Combine(
            uploadsFolder,
            fileName
        );

        await using var stream =
            new FileStream(filePath, FileMode.Create);

        await file.CopyToAsync(stream);

        var imageUrl = $"/uploads/{fileName}";

        return Ok(new
        {
            imageUrl
        });
    }
}