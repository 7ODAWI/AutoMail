using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AutoMail.Web.Hubs
{
    /// <summary>
    /// SignalR hub for real-time email operation progress updates.
    /// Clients join a group named "op-{operationId}" to receive live updates
    /// for that specific operation.
    /// </summary>
    public class EmailOperationHub : Hub
    {
        /// <summary>
        /// Client calls this to subscribe to live updates for a specific operation.
        /// </summary>
        public async Task SubscribeToOperation(long operationId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"op-{operationId}");
        }

        /// <summary>
        /// Client calls this to unsubscribe from a specific operation's updates.
        /// </summary>
        public async Task UnsubscribeFromOperation(long operationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"op-{operationId}");
        }
    }

    /// <summary>
    /// Notification pushed to clients after each individual email send attempt.
    /// </summary>
    public class EmailSentNotification
    {
        public long OperationId { get; set; }

        /// <summary>Recipient email address.</summary>
        public string Email { get; set; }

        /// <summary>"Sent" or "Failed"</summary>
        public string Status { get; set; }

        public string SentAt { get; set; }

        /// <summary>Email of the SMTP sender account used.</summary>
        public string SenderEmail { get; set; }

        public string ErrorMessage { get; set; }

        // Running totals (updated after each send)
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int TotalEmails { get; set; }
    }

    /// <summary>
    /// Notification pushed when operation overall status changes (Paused, Cancelled, Completed, etc.)
    /// </summary>
    public class OperationStatusChangedNotification
    {
        public long OperationId { get; set; }
        public string Status { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int PendingCount { get; set; }
        public int TotalEmails { get; set; }
    }
}
