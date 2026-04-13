using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMail.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSenderAndBulkEmailLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "BulkEmails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "EmailSenders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Password = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SmtpHost = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    EnableSsl = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DailyLimit = table.Column<int>(type: "int", nullable: false),
                    DelayBetweenEmailsMs = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeleterUserId = table.Column<long>(type: "bigint", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSenders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BulkEmailLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BulkEmailId = table.Column<long>(type: "bigint", nullable: false),
                    EmailAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SenderId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SentTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
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
                name: "IX_EmailSenders_Email",
                table: "EmailSenders",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BulkEmailLogs");

            migrationBuilder.DropTable(
                name: "EmailSenders");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "BulkEmails");
        }
    }
}
