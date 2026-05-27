using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMail.Migrations
{
    /// <inheritdoc />
    public partial class MakeOperationIdNullableForSharedTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiGeneratedTemplateVersions_EmailOperations_OperationId",
                table: "AiGeneratedTemplateVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_AiGenerationRuns_EmailOperations_OperationId",
                table: "AiGenerationRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_EmailOperations_OperationId",
                table: "EmailTemplates");

            migrationBuilder.AlterColumn<long>(
                name: "OperationId",
                table: "EmailTemplates",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "OperationId",
                table: "AiGenerationRuns",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "OperationId",
                table: "AiGeneratedTemplateVersions",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddForeignKey(
                name: "FK_AiGeneratedTemplateVersions_EmailOperations_OperationId",
                table: "AiGeneratedTemplateVersions",
                column: "OperationId",
                principalTable: "EmailOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AiGenerationRuns_EmailOperations_OperationId",
                table: "AiGenerationRuns",
                column: "OperationId",
                principalTable: "EmailOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_EmailOperations_OperationId",
                table: "EmailTemplates",
                column: "OperationId",
                principalTable: "EmailOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiGeneratedTemplateVersions_EmailOperations_OperationId",
                table: "AiGeneratedTemplateVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_AiGenerationRuns_EmailOperations_OperationId",
                table: "AiGenerationRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_EmailOperations_OperationId",
                table: "EmailTemplates");

            migrationBuilder.AlterColumn<long>(
                name: "OperationId",
                table: "EmailTemplates",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "OperationId",
                table: "AiGenerationRuns",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "OperationId",
                table: "AiGeneratedTemplateVersions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiGeneratedTemplateVersions_EmailOperations_OperationId",
                table: "AiGeneratedTemplateVersions",
                column: "OperationId",
                principalTable: "EmailOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AiGenerationRuns_EmailOperations_OperationId",
                table: "AiGenerationRuns",
                column: "OperationId",
                principalTable: "EmailOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_EmailOperations_OperationId",
                table: "EmailTemplates",
                column: "OperationId",
                principalTable: "EmailOperations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
