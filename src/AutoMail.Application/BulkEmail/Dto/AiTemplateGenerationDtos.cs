using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class GenerateAiTemplatesInput
    {
        [Required]
        public long OperationId { get; set; }

        [Range(1, 10000000)]
        public int VariantCount { get; set; } = 3;

        [MaxLength(4000)]
        public string Prompt { get; set; }

        [MaxLength(128)]
        public string Tone { get; set; }
    }

    /// <summary>
    /// Input for standalone global AI template generation — not tied to any operation.
    /// Generated templates go into the shared pool used by all send operations.
    /// </summary>
    public class GenerateGlobalAiTemplatesInput
    {
        [Range(1, 10000000)]
        public int VariantCount { get; set; } = 10;

        [MaxLength(20000)]
        public string Prompt { get; set; }

        [MaxLength(128)]
        public string Tone { get; set; }
    }

    public class AiTemplateVariantDto
    {
        public string Subject { get; set; }
        public string PreviewText { get; set; }
        public string BodyHtml { get; set; }
        public int Weight { get; set; }
        public string OutlineJson { get; set; }
        public string ComponentOrderJson { get; set; }
        public string ModelName { get; set; }
        public string PromptVersion { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public int? LatencyMs { get; set; }
        public double SimilarityScore { get; set; }
    }

    public class GenerateAiTemplatesResultDto
    {
        public long GenerationRunId { get; set; }
        public string Status { get; set; }
        public int RequestedCount { get; set; }
        public int GeneratedCount { get; set; }
        public List<EmailTemplateDto> Templates { get; set; } = new();
    }
}