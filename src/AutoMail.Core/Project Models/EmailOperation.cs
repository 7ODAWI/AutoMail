using Abp.Domain.Entities.Auditing;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoMail.Project_Models
{
    [Table("EmailOperations")]
    public class EmailOperation : CreationAuditedEntity<long>
    {
        public const int MaxSubjectLength = 500;
        public const int MaxAiPromptLength = 4000;
        public const int MaxAiToneLength = 128;

        [Required]
        [MaxLength(MaxSubjectLength)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        public OperationStatus Status { get; set; } = OperationStatus.Pending;

        public int TotalEmails { get; set; }

        public int SentCount { get; set; }

        public int FailedCount { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        /// <summary>Human-readable reason why the operation stopped early (null = normal completion).</summary>
        [MaxLength(1000)]
        public string StopReason { get; set; }

        public AiGenerationMode AiGenerationMode { get; set; } = AiGenerationMode.Disabled;

        [MaxLength(MaxAiPromptLength)]
        public string AiPrompt { get; set; }

        [Range(0, 20)]
        public int AiVariantCount { get; set; }

        [MaxLength(MaxAiToneLength)]
        public string AiTone { get; set; }

        public DateTime? AiLastGeneratedAt { get; set; }
    }

    [Table("EmailTemplates")]
    public class EmailTemplate : CreationAuditedEntity<long>
    {
        [MaxLength(200)]
        public string Name { get; set; }

        public long OperationId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        [Range(1, 100)]
        public int Weight { get; set; } = 1;

        public bool IsAiGenerated { get; set; }

        [MaxLength(500)]
        public string PreviewText { get; set; }

        public long? AiGenerationRunId { get; set; }

        public long? AiGeneratedVersionId { get; set; }

        public double? SimilarityScore { get; set; }
    }
}
