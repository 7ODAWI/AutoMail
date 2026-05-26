using System;
using AutoMail.Project_Models;

namespace AutoMail.BulkEmail.Dto
{
    public class OperationListDto
    {
        public long Id { get; set; }
        public string Subject { get; set; }
        public string StatusText { get; set; }
        public int TotalEmails { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string StopReason { get; set; }
        public AiGenerationMode AiGenerationMode { get; set; }
        public int AiVariantCount { get; set; }
        public DateTime? AiLastGeneratedAt { get; set; }
    }
}
