using Microsoft.AspNetCore.Http;
using AutoMail.Project_Models;
using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class CreateOperationInput
    {
        [Required]
        public IFormFile File { get; set; }

        public int EmailColumnIndex { get; set; } = 0;

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        public AiGenerationMode AiGenerationMode { get; set; } = AiGenerationMode.PreGeneratedPool;

        [Range(0, 20)]
        public int AiVariantCount { get; set; }

        [MaxLength(4000)]
        public string AiPrompt { get; set; }

        [MaxLength(128)]
        public string AiTone { get; set; }

        /// <summary>When false, the operation is saved as a draft without starting the background job.</summary>
        public bool StartImmediately { get; set; } = true;
    }
}
