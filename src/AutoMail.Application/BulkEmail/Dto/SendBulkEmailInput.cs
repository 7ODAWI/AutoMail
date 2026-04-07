using System.ComponentModel.DataAnnotations;

namespace AutoMail.BulkEmail.Dto
{
    public class SendBulkEmailInput
    {
        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }
    }
}
