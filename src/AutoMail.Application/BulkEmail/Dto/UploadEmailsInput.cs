using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class UploadEmailsInput
    {
        /// <summary>
        /// The uploaded Excel (.xlsx) or CSV file.
        /// </summary>
        [Required]
        public IFormFile File { get; set; }

        /// <summary>
        /// Zero-based column index that contains the email addresses.
        /// Defaults to 0 (first column).
        /// </summary>
        public int EmailColumnIndex { get; set; } = 0;
    }
}
