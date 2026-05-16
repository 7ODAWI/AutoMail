using Abp.Domain.Entities.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;
using System.ComponentModel.DataAnnotations;

namespace AutoMail.Project_Models
{
    [Table("EmailSenders")]
    public class EmailSender : FullAuditedEntity<int>
    {
        public const int MaxEmailLength = 256;
        public const int MaxPasswordLength = 512;
        public const int MaxDisplayNameLength = 128;
        public const int MaxSmtpHostLength = 256;

        [Required]
        [MaxLength(MaxEmailLength)]
        public string Email { get; set; }

        [Required]
        [MaxLength(MaxPasswordLength)]
        public string Password { get; set; }

        [MaxLength(MaxDisplayNameLength)]
        public string DisplayName { get; set; }

        [MaxLength(MaxSmtpHostLength)]
        public string SmtpHost { get; set; } = "smtp.gmail.com";

        public int SmtpPort { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public bool IsActive { get; set; } = true;

        /// <summary>Maximum emails this sender can send per calendar day.</summary>
        public int DailyLimit { get; set; } = 500;

        /// <summary>Milliseconds to wait between sending each email.</summary>
        public int DelayBetweenEmailsMs { get; set; } = 1000;

        /// <summary>
        /// If set, sender is blocked from sending until this UTC date/time (exclusive).
        /// Used to temporarily block problematic senders (e.g. daily limit exceeded).
        /// </summary>
        public DateTime? BlockedUntilUtc { get; set; }

        [MaxLength(1024)]
        public string BlockReason { get; set; }
    }
}
