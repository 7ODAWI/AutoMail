using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    /// <summary>
    /// Abstraction for pushing real-time email operation progress to connected clients.
    /// Implemented in the Web layer via SignalR; a no-op implementation is registered by default.
    /// </summary>
    public interface IEmailOperationNotifier
    {
        /// <summary>
        /// Called after each individual email is sent or fails.
        /// </summary>
        Task NotifyEmailSentAsync(EmailSentEvent ev);

        /// <summary>
        /// Called when the operation status changes (Paused, Cancelled, Completed, etc.).
        /// </summary>
        Task NotifyStatusChangedAsync(OperationStatusChangedEvent ev);
    }

    public class EmailSentEvent
    {
        public long OperationId { get; set; }
        public string Email { get; set; }
        /// <summary>"Sent" or "Failed"</summary>
        public string Status { get; set; }
        public string SentAt { get; set; }
        public string SenderEmail { get; set; }
        public string ErrorMessage { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int TotalEmails { get; set; }
    }

    public class OperationStatusChangedEvent
    {
        public long OperationId { get; set; }
        public string Status { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int TotalEmails { get; set; }
    }

    /// <summary>
    /// Default no-op notifier — used when SignalR is not wired up.
    /// </summary>
    public class NullEmailOperationNotifier : IEmailOperationNotifier
    {
        public static readonly NullEmailOperationNotifier Instance = new NullEmailOperationNotifier();

        public Task NotifyEmailSentAsync(EmailSentEvent ev) => Task.CompletedTask;
        public Task NotifyStatusChangedAsync(OperationStatusChangedEvent ev) => Task.CompletedTask;
    }
}
