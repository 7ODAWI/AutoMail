using Microsoft.AspNetCore.Http;
using AutoMail.Project_Models;
using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class UpdateOperationInput
    {
        public long Id { get; set; }

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        public AiGenerationMode AiGenerationMode { get; set; } = AiGenerationMode.Disabled;

        [Range(0, 20)]
        public int AiVariantCount { get; set; }

        [MaxLength(4000)]
        public string AiPrompt { get; set; }

        [MaxLength(128)]
        public string AiTone { get; set; }

        /// <summary>Optional — if provided, replaces all existing pending recipients.</summary>
        public IFormFile File { get; set; }

        public int EmailColumnIndex { get; set; } = 0;
    }
}
