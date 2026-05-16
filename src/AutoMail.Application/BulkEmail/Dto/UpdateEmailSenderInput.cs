using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class UpdateEmailSenderInput
    {
        [Required]
        public int Id { get; set; }

        [Required]
        [MaxLength(256)]
        [EmailAddress]
        public string Email { get; set; }

        /// <summary>Leave blank to keep the existing password.</summary>
        [MaxLength(512)]
        public string Password { get; set; }

        [MaxLength(128)]
        public string DisplayName { get; set; }

        [MaxLength(256)]
        public string SmtpHost { get; set; } = "smtp.gmail.com";

        public int SmtpPort { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public bool IsActive { get; set; } = true;

        [Range(1, 10000)]
        public int DailyLimit { get; set; } = 500;

        [Range(0, 1800000)]
        public int DelayBetweenEmailsMs { get; set; } = 1000;
    }
}
