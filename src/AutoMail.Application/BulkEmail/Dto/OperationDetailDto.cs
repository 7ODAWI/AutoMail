using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoMail.Project_Models;

namespace AutoMail.BulkEmail.Dto
{
    public class OperationDetailDto
    {
        public long Id { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string StatusText { get; set; }
        public int TotalEmails { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int RetryableCount { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string StopReason { get; set; }
        public AiGenerationMode AiGenerationMode { get; set; }
        public int AiVariantCount { get; set; }
        public string AiPrompt { get; set; }
        public string AiTone { get; set; }
        public DateTime? AiLastGeneratedAt { get; set; }
        public List<EmailTemplateDto> Templates { get; set; } = new();
        public List<OperationEmailDto> Emails { get; set; } = new();
    }

    public class EmailTemplateDto
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string PreviewText { get; set; }
        public int Weight { get; set; }
        public bool IsAiGenerated { get; set; }
        public double? SimilarityScore { get; set; }
        public long? AiGenerationRunId { get; set; }
        public long? AiGeneratedVersionId { get; set; }
    }

    public class CreateTemplateInput
    {
        public long OperationId { get; set; }
        [MaxLength(200)] public string Name { get; set; }
        [Required] public string Subject { get; set; }
        [Required] public string Body { get; set; }
        [MaxLength(500)] public string PreviewText { get; set; }
        [Range(1, 100)] public int Weight { get; set; } = 1;
        public bool IsAiGenerated { get; set; }
        public double? SimilarityScore { get; set; }
        public long? AiGenerationRunId { get; set; }
        public long? AiGeneratedVersionId { get; set; }
    }

    public class UpdateTemplateInput
    {
        public long Id { get; set; }
        [MaxLength(200)] public string Name { get; set; }
        [Required] public string Subject { get; set; }
        [Required] public string Body { get; set; }
        [MaxLength(500)] public string PreviewText { get; set; }
        [Range(1, 100)] public int Weight { get; set; } = 1;
        public bool IsAiGenerated { get; set; }
        public double? SimilarityScore { get; set; }
        public long? AiGenerationRunId { get; set; }
        public long? AiGeneratedVersionId { get; set; }
    }
}
