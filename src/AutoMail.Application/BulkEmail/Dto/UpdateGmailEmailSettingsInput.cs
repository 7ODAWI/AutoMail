using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class UpdateGmailEmailSettingsInput
    {
        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string UserName { get; set; }

        [MaxLength(512)]
        public string Password { get; set; }

        [Required]
        [EmailAddress]
        [MaxLength(256)]
        public string DefaultFromAddress { get; set; }

        [Required]
        [MaxLength(128)]
        public string DefaultFromDisplayName { get; set; }
    }
}
