using System.Security.Claims;
using System.Text.RegularExpressions;
using job_application_tracker_api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace job_application_tracker_api.Controllers;

[Authorize]
[ApiController]
public sealed class ImagesController(ApplicationDbContext database, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("/uploads/{fileName}")]
    public async Task<IActionResult> GetImage(string fileName, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(fileName, @"\A[a-zA-Z0-9_-]+\.(jpg|jpeg|png|webp)\z", RegexOptions.IgnoreCase))
            return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var imageUrl = $"/uploads/{fileName}";
        var owned = await database.UploadedImages.AnyAsync(image => image.FileName == fileName && image.UserId == userId, cancellationToken)
            || await database.JobApplications.AnyAsync(application => application.UserId == userId && application.ImageUrl == imageUrl, cancellationToken);
        if (!owned) return NotFound();

        var path = Path.Combine(environment.ContentRootPath, "App_Data", "uploads", fileName);
        if (!System.IO.File.Exists(path)) path = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg"
        };
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return PhysicalFile(path, contentType);
    }
}
