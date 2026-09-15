using job_application_tracker_api.Models;
using Microsoft.EntityFrameworkCore;

namespace job_application_tracker_api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options
    ) : base(options)
    {
    }

    public DbSet<JobApplication> JobApplications { get; set; }
}