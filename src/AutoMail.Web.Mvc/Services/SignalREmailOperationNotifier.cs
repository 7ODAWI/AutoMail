using Abp.Dependency;
using AutoMail.BulkEmail;
using AutoMail.Web.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace AutoMail.Web.Services
{
    /// <summary>
    /// SignalR-backed implementation of IEmailOperationNotifier.
    /// Pushes real-time events to clients subscribed to each operation's group.
    /// </summary>
    public class SignalREmailOperationNotifier : IEmailOperationNotifier, ITransientDependency
    {
        private readonly IHubContext<EmailOperationHub> _hubContext;

        public SignalREmailOperationNotifier(IHubContext<EmailOperationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyEmailSentAsync(EmailSentEvent ev)
        {
            await _hubContext.Clients
                .Group($"op-{ev.OperationId}")
                .SendAsync("OnEmailSent", new EmailSentNotification
                {
                    OperationId = ev.OperationId,
                    Email = ev.Email,
                    Status = ev.Status,
                    SentAt = ev.SentAt,
                    SenderEmail = ev.SenderEmail,
                    ErrorMessage = ev.ErrorMessage,
                    SentCount = ev.SentCount,
                    FailedCount = ev.FailedCount,
                    PendingCount = ev.PendingCount,
                    TotalEmails = ev.TotalEmails
                });
        }

        public async Task NotifyStatusChangedAsync(OperationStatusChangedEvent ev)
        {
            await _hubContext.Clients
                .Group($"op-{ev.OperationId}")
                .SendAsync("OnStatusChanged", new OperationStatusChangedNotification
                {
                    OperationId = ev.OperationId,
                    Status = ev.Status,
                    SentCount = ev.SentCount,
                    FailedCount = ev.FailedCount,
                    PendingCount = ev.PendingCount,
                    TotalEmails = ev.TotalEmails
                });
        }
    }
}
