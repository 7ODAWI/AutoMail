using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMail.EntityFrameworkCore.Migrations
{
    public partial class AddAiTemplatesGeneratedFlag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiTemplatesGenerated",
                table: "EmailOperations",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiTemplatesGenerated",
                table: "EmailOperations");
        }
    }
}