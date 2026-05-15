using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMail.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "TemplateId",
                table: "OperationEmails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmailTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailTemplates_EmailOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "EmailOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationEmails_TemplateId",
                table: "OperationEmails",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_OperationId",
                table: "EmailTemplates",
                column: "OperationId");

            migrationBuilder.AddForeignKey(
                name: "FK_OperationEmails_EmailTemplates_TemplateId",
                table: "OperationEmails",
                column: "TemplateId",
                principalTable: "EmailTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OperationEmails_EmailTemplates_TemplateId",
                table: "OperationEmails");

            migrationBuilder.DropTable(
                name: "EmailTemplates");

            migrationBuilder.DropIndex(
                name: "IX_OperationEmails_TemplateId",
                table: "OperationEmails");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "OperationEmails");
        }
    }
}
