using Microsoft.AspNetCore.Http;
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

        /// <summary>When false, the operation is saved as a draft without starting the background job.</summary>
        public bool StartImmediately { get; set; } = true;
    }
}
