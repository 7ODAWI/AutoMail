using Abp.Domain.Entities.Auditing;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoMail.Project_Models
{
    [Table("AiGenerationRuns")]
    public class AiGenerationRun : CreationAuditedEntity<long>
    {
        [Required]
        public long OperationId { get; set; }

        public AiGenerationRunStatus Status { get; set; } = AiGenerationRunStatus.Pending;

        [Range(1, 10000000)]
        public int RequestedVariants { get; set; }

        public int GeneratedVariants { get; set; }

        [MaxLength(128)]
        public string ModelRoute { get; set; }

        [MaxLength(128)]
        public string CorrelationId { get; set; }

        [MaxLength(2000)]
        public string ErrorMessage { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    [Table("AiGeneratedTemplateVersions")]
    public class AiGeneratedTemplateVersion : CreationAuditedEntity<long>
    {
        [Required]
        public long OperationId { get; set; }

        [Required]
        public long GenerationRunId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [MaxLength(500)]
        public string PreviewText { get; set; }

        [Required]
        public string BodyHtml { get; set; }

        public string OutlineJson { get; set; }

        public string ComponentOrderJson { get; set; }

        [MaxLength(128)]
        public string ModelName { get; set; }

        [MaxLength(128)]
        public string PromptVersion { get; set; }

        public int? InputTokens { get; set; }

        public int? OutputTokens { get; set; }

        public int? LatencyMs { get; set; }

        [MaxLength(128)]
        public string SubjectHash { get; set; }

        [MaxLength(128)]
        public string BodyHash { get; set; }

        [MaxLength(128)]
        public string StructureHash { get; set; }

        public double SimilarityScore { get; set; }
    }

    [Table("AiTemplateProfiles")]
    public class AiTemplateProfile : CreationAuditedEntity<long>
    {
        [Required]
        [MaxLength(128)]
        public string ProfileKey { get; set; }

        [Required]
        public string ProfileJson { get; set; }

        [MaxLength(128)]
        public string SourceFingerprint { get; set; }

        public DateTime GeneratedAt { get; set; }

        public int SourceTemplateCount { get; set; }
    }
}