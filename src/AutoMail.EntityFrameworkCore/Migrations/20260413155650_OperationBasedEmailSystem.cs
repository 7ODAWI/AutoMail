using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMail.Migrations
{
    /// <inheritdoc />
    public partial class OperationBasedEmailSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkEmailLogs");

            migrationBuilder.DropTable(
                name: "BulkEmails");

            migrationBuilder.CreateTable(
                name: "EmailOperations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    TotalEmails = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperationEmails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SenderId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperationEmails_EmailOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "EmailOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OperationEmails_EmailSenders_SenderId",
                        column: x => x.SenderId,
                        principalTable: "EmailSenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationEmails_OperationId_Status",
                table: "OperationEmails",
                columns: new[] { "OperationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationEmails_SenderId",
                table: "OperationEmails",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationEmails");

            migrationBuilder.DropTable(
                name: "EmailOperations");

            migrationBuilder.CreateTable(
                name: "BulkEmails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkEmails", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BulkEmailLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BulkEmailId = table.Column<long>(type: "bigint", nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BulkEmailLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BulkEmailLogs_BulkEmails_BulkEmailId",
                        column: x => x.BulkEmailId,
                        principalTable: "BulkEmails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BulkEmailLogs_EmailSenders_SenderId",
                        column: x => x.SenderId,
                        principalTable: "EmailSenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BulkEmailLogs_BulkEmailId",
                table: "BulkEmailLogs",
                column: "BulkEmailId");

            migrationBuilder.CreateIndex(
                name: "IX_BulkEmailLogs_SenderId_Status_SentTime",
                table: "BulkEmailLogs",
                columns: new[] { "SenderId", "Status", "SentTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BulkEmailLogs_Status",
                table: "BulkEmailLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BulkEmails_Email",
                table: "BulkEmails",
                column: "Email",
                unique: true);
        }
    }
}
