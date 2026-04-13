using Abp.Domain.Entities.Auditing;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoMail.Project_Models
{
    /// <summary>
    /// Immutable log record for each send attempt.
    /// Each attempt (including retries) creates a NEW record — never updated.
    /// </summary>
    [Table("BulkEmailLogs")]
    public class BulkEmailLog : CreationAuditedEntity<long>
    {
        public const int MaxEmailLength = 256;
        public const int MaxErrorMessageLength = 2000;

        public long BulkEmailId { get; set; }

        [Required]
        [MaxLength(MaxEmailLength)]
        public string EmailAddress { get; set; }

        public int SenderId { get; set; }

        public SendStatus Status { get; set; }

        [MaxLength(MaxErrorMessageLength)]
        public string ErrorMessage { get; set; }

        public DateTime? SentTime { get; set; }

        /// <summary>1-based attempt number for this email address.</summary>
        public int AttemptNumber { get; set; }

        [ForeignKey(nameof(BulkEmailId))]
        public virtual BulkEmail BulkEmail { get; set; }

        [ForeignKey(nameof(SenderId))]
        public virtual EmailSender EmailSender { get; set; }
    }
}
