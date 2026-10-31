using job_application_tracker_api.Contracts;
using job_application_tracker_api.Data;
using job_application_tracker_api.Models;
using job_application_tracker_api.Uploads;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace job_application_tracker_api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobApplicationsController(ApplicationDbContext context, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<JobApplicationResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var applications = await context.JobApplications.AsNoTracking()
            .OrderBy(application => application.Id).ToListAsync(cancellationToken);
        return Ok(applications.Select(JobApplicationResponse.FromEntity).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<JobApplicationResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var application = await context.JobApplications.AsNoTracking()
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
        return application is null
            ? ApplicationNotFound()
            : Ok(JobApplicationResponse.FromEntity(application));
    }

    [HttpPost]
    public async Task<ActionResult<JobApplicationResponse>> Create(
        JobApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = new JobApplication();
        ApplyRequest(application, request);
        context.JobApplications.Add(application);
        await context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = application.Id },
            JobApplicationResponse.FromEntity(application));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<JobApplicationResponse>> Update(
        int id, JobApplicationRequest request, CancellationToken cancellationToken)
    {
        var application = await context.JobApplications
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
        if (application is null) return ApplicationNotFound();

        ApplyRequest(application, request);
        await context.SaveChangesAsync(cancellationToken);
        return Ok(JobApplicationResponse.FromEntity(application));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var application = await context.JobApplications
            .FirstOrDefaultAsync(application => application.Id == id, cancellationToken);
        if (application is null) return ApplicationNotFound();

        context.JobApplications.Remove(application);
        await context.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("uploadimage")]
    [UploadRequestLimit]
    [RequestSizeLimit(ImageUploadPolicy.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUploadPolicy.MaxRequestBytes)]
    public async Task<ActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(statusCode: 400, title: "Ingen bild valdes.");
        }
        if (file.Length > ImageUploadPolicy.MaxFileBytes)
        {
            return Problem(statusCode: 413, title: "Bilden får vara högst 5 MiB.");
        }

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var bytesRead = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false,
            cancellationToken: cancellationToken);
        var extension = ImageUploadPolicy.GetExtension(header.AsSpan(0, bytesRead), file.FileName);
        if (extension is null)
        {
            return Problem(statusCode: 400, title: "Välj en JPEG-, PNG- eller WebP-bild med rätt filändelse.");
        }

        var uploadsFolder = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);
        try
        {
            await using var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write);
            await output.WriteAsync(header.AsMemory(0, bytesRead), cancellationToken);
            await input.CopyToAsync(output, cancellationToken);
        }
        catch
        {
            System.IO.File.Delete(filePath);
            throw;
        }

        return Ok(new { imageUrl = $"/uploads/{fileName}" });
    }

    private ObjectResult ApplicationNotFound() =>
        Problem(statusCode: 404, title: "Jobbansökan hittades inte.");

    private static void ApplyRequest(JobApplication application, JobApplicationRequest request)
    {
        application.Company = request.Company.Trim();
        application.Position = request.Position.Trim();
        application.Location = request.Location.Trim();
        application.DateApplied = request.Status == "Intresserad" ? null : request.DateApplied;
        application.Status = request.Status;
        application.Notes = request.Notes?.Trim() ?? string.Empty;
        application.ImageUrl = string.IsNullOrEmpty(request.ImageUrl) ? null : request.ImageUrl;
    }
}
