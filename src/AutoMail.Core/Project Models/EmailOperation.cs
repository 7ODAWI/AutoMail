using Abp.Domain.Entities.Auditing;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoMail.Project_Models
{
    [Table("EmailOperations")]
    public class EmailOperation : CreationAuditedEntity<long>
    {
        public const int MaxSubjectLength = 500;

        [Required]
        [MaxLength(MaxSubjectLength)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        public OperationStatus Status { get; set; } = OperationStatus.Pending;

        public int TotalEmails { get; set; }

        public int SentCount { get; set; }

        public int FailedCount { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }
    }
}
