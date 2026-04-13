using System;
using System.Collections.Generic;

namespace AutoMail.BulkEmail.Dto
{
    public class OperationDetailDto
    {
        public long Id { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string StatusText { get; set; }
        public int TotalEmails { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int RetryableCount { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<OperationEmailDto> Emails { get; set; } = new();
    }
}
