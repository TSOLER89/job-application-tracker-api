using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace job_application_tracker_api.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationPlanningDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ApplicationDeadline",
                table: "JobApplications",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "FollowUpDate",
                table: "JobApplications",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicationDeadline",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "FollowUpDate",
                table: "JobApplications");
        }
    }
}
