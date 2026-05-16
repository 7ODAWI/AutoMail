using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitHubScraper.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GitHubToken = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    KeywordsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MinFollowers = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProfilesScanned = table.Column<long>(type: "bigint", nullable: false),
                    EmailsFound = table.Column<long>(type: "bigint", nullable: false),
                    Failures = table.Column<long>(type: "bigint", nullable: false),
                    CurrentUser = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CurrentQuery = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CheckpointJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeveloperResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Bio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    EmailSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmailConfidence = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Website = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Followers = table.Column<int>(type: "int", nullable: false),
                    Repos = table.Column<int>(type: "int", nullable: false),
                    ProfileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FoundAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeveloperResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeveloperResults_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperResults_OperationId_Email",
                table: "DeveloperResults",
                columns: new[] { "OperationId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_DeveloperResults_OperationId_Username",
                table: "DeveloperResults",
                columns: new[] { "OperationId", "Username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeveloperResults");

            migrationBuilder.DropTable(
                name: "Operations");
        }
    }
}
