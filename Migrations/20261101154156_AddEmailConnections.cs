using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace job_application_tracker_api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailConnectionAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SessionHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailConnectionAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailConnectionAttempts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmailConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    ProtectedTokens = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailConnections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailConnectionAttempts_ExpiresAt",
                table: "EmailConnectionAttempts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailConnectionAttempts_TicketHash",
                table: "EmailConnectionAttempts",
                column: "TicketHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailConnectionAttempts_UserId",
                table: "EmailConnectionAttempts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailConnections_UserId_Provider_ProviderAccountId",
                table: "EmailConnections",
                columns: new[] { "UserId", "Provider", "ProviderAccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailConnectionAttempts");

            migrationBuilder.DropTable(
                name: "EmailConnections");
        }
    }
}
