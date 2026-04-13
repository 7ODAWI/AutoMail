using System;

namespace AutoMail.BulkEmail.Dto
{
    public class OperationEmailDto
    {
        public string Email { get; set; }
        public string StatusText { get; set; }
        public int RetryCount { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime? SentAt { get; set; }
        public string SenderEmail { get; set; }
    }
}
