using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoMail.Migrations
{
    /// <inheritdoc />
    public partial class AddAiContentEngineFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AiGeneratedVersionId",
                table: "EmailTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AiGenerationRunId",
                table: "EmailTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAiGenerated",
                table: "EmailTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreviewText",
                table: "EmailTemplates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SimilarityScore",
                table: "EmailTemplates",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "AiGenerationMode",
                table: "EmailOperations",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AiLastGeneratedAt",
                table: "EmailOperations",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiPrompt",
                table: "EmailOperations",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiTone",
                table: "EmailOperations",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiVariantCount",
                table: "EmailOperations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AiGenerationRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedVariants = table.Column<int>(type: "int", nullable: false),
                    GeneratedVariants = table.Column<int>(type: "int", nullable: false),
                    ModelRoute = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGenerationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGenerationRuns_EmailOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "EmailOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiTemplateProfiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProfileKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceTemplateCount = table.Column<int>(type: "int", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiTemplateProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiGeneratedTemplateVersions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OperationId = table.Column<long>(type: "bigint", nullable: false),
                    GenerationRunId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PreviewText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BodyHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OutlineJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComponentOrderJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModelName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: true),
                    OutputTokens = table.Column<int>(type: "int", nullable: true),
                    LatencyMs = table.Column<int>(type: "int", nullable: true),
                    SubjectHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BodyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    StructureHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SimilarityScore = table.Column<double>(type: "float", nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorUserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiGeneratedTemplateVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiGeneratedTemplateVersions_AiGenerationRuns_GenerationRunId",
                        column: x => x.GenerationRunId,
                        principalTable: "AiGenerationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AiGeneratedTemplateVersions_EmailOperations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "EmailOperations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_AiGeneratedVersionId",
                table: "EmailTemplates",
                column: "AiGeneratedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_AiGenerationRunId",
                table: "EmailTemplates",
                column: "AiGenerationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedTemplateVersions_BodyHash",
                table: "AiGeneratedTemplateVersions",
                column: "BodyHash");

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedTemplateVersions_GenerationRunId",
                table: "AiGeneratedTemplateVersions",
                column: "GenerationRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedTemplateVersions_OperationId_CreationTime",
                table: "AiGeneratedTemplateVersions",
                columns: new[] { "OperationId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedTemplateVersions_StructureHash",
                table: "AiGeneratedTemplateVersions",
                column: "StructureHash");

            migrationBuilder.CreateIndex(
                name: "IX_AiGeneratedTemplateVersions_SubjectHash",
                table: "AiGeneratedTemplateVersions",
                column: "SubjectHash");

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationRuns_OperationId_CreationTime",
                table: "AiGenerationRuns",
                columns: new[] { "OperationId", "CreationTime" });

            migrationBuilder.CreateIndex(
                name: "IX_AiTemplateProfiles_ProfileKey",
                table: "AiTemplateProfiles",
                column: "ProfileKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiTemplateProfiles_SourceFingerprint",
                table: "AiTemplateProfiles",
                column: "SourceFingerprint");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_AiGeneratedTemplateVersions_AiGeneratedVersionId",
                table: "EmailTemplates",
                column: "AiGeneratedVersionId",
                principalTable: "AiGeneratedTemplateVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_EmailTemplates_AiGenerationRuns_AiGenerationRunId",
                table: "EmailTemplates",
                column: "AiGenerationRunId",
                principalTable: "AiGenerationRuns",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_AiGeneratedTemplateVersions_AiGeneratedVersionId",
                table: "EmailTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailTemplates_AiGenerationRuns_AiGenerationRunId",
                table: "EmailTemplates");

            migrationBuilder.DropTable(
                name: "AiGeneratedTemplateVersions");

            migrationBuilder.DropTable(
                name: "AiTemplateProfiles");

            migrationBuilder.DropTable(
                name: "AiGenerationRuns");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_AiGeneratedVersionId",
                table: "EmailTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_AiGenerationRunId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "AiGeneratedVersionId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "AiGenerationRunId",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "IsAiGenerated",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "PreviewText",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "SimilarityScore",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "AiGenerationMode",
                table: "EmailOperations");

            migrationBuilder.DropColumn(
                name: "AiLastGeneratedAt",
                table: "EmailOperations");

            migrationBuilder.DropColumn(
                name: "AiPrompt",
                table: "EmailOperations");

            migrationBuilder.DropColumn(
                name: "AiTone",
                table: "EmailOperations");

            migrationBuilder.DropColumn(
                name: "AiVariantCount",
                table: "EmailOperations");
        }
    }
}
