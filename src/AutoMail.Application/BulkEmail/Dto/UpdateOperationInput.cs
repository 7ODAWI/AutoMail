using Microsoft.AspNetCore.Http;
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

        /// <summary>Optional — if provided, replaces all existing pending recipients.</summary>
        public IFormFile File { get; set; }

        public int EmailColumnIndex { get; set; } = 0;
    }
}
