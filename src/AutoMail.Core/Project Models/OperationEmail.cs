using Abp.Domain.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoMail.Project_Models
{
    [Table("OperationEmails")]
    public class OperationEmail : Entity<long>
    {
        public const int MaxEmailLength = 256;
        public const int MaxErrorMessageLength = 2000;

        public long OperationId { get; set; }

        [Required]
        [MaxLength(MaxEmailLength)]
        public string Email { get; set; }

        public SendStatus Status { get; set; } = SendStatus.Pending;

        public int RetryCount { get; set; }

        [MaxLength(MaxErrorMessageLength)]
        public string ErrorMessage { get; set; }

        public DateTime? SentAt { get; set; }

        public int? SenderId { get; set; }

        [ForeignKey(nameof(OperationId))]
        public virtual EmailOperation Operation { get; set; }

        [ForeignKey(nameof(SenderId))]
        public virtual EmailSender Sender { get; set; }
    }
}
