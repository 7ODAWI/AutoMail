using Abp.Domain.Entities.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoMail.Project_Models
{
    /// <summary>
    /// Stores email addresses imported from uploaded files.
    /// CreationAuditedEntity automatically tracks CreationTime and CreatorUserId.
    /// </summary>
    [Table("BulkEmails")]
    public class BulkEmail : CreationAuditedEntity<long>
    {
        public const int MaxEmailLength = 256;

        [Required]
        [MaxLength(MaxEmailLength)]
        public string Email { get; set; }

        /// <summary>Whether the email has been sent via a background job.</summary>
        public bool IsSent { get; set; }
    }
}
