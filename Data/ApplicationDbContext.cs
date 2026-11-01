using job_application_tracker_api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace job_application_tracker_api.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options
    ) : base(options)
    {
    }

    public DbSet<JobApplication> JobApplications { get; set; }
    public DbSet<AuthSession> AuthSessions { get; set; }
    public DbSet<UploadedImage> UploadedImages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<JobApplication>().HasOne<IdentityUser>().WithMany()
            .HasForeignKey(application => application.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AuthSession>().HasKey(session => session.TokenHash);
        builder.Entity<AuthSession>().Property(session => session.TokenHash).HasMaxLength(64);
        builder.Entity<AuthSession>().HasOne(session => session.User).WithMany()
            .HasForeignKey(session => session.UserId);
        builder.Entity<AuthSession>().HasIndex(session => session.ExpiresAt);
        builder.Entity<UploadedImage>().HasKey(image => image.FileName);
        builder.Entity<UploadedImage>().Property(image => image.FileName).HasMaxLength(64);
        builder.Entity<UploadedImage>().HasOne<IdentityUser>().WithMany().HasForeignKey(image => image.UserId);
    }
}
