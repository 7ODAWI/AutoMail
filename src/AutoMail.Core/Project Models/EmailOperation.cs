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

        /// <summary>Human-readable reason why the operation stopped early (null = normal completion).</summary>
        [MaxLength(1000)]
        public string StopReason { get; set; }
    }

    [Table("EmailTemplates")]
    public class EmailTemplate : CreationAuditedEntity<long>
    {
        [MaxLength(200)]
        public string Name { get; set; }

        public long OperationId { get; set; }

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        [Range(1, 100)]
        public int Weight { get; set; } = 1;
    }
}
