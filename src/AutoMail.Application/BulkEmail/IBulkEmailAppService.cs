using Abp.Application.Services;
using AutoMail.BulkEmail.Dto;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public interface IBulkEmailAppService : IApplicationService
    {
        /// <summary>
        /// Parses the uploaded file, validates emails and silently persists
        /// new unique addresses to the database.
        /// </summary>
        Task<UploadEmailsResult> UploadAndStoreEmailsAsync(UploadEmailsInput input);

        /// <summary>
        /// Enqueues a background job that will send emails to all unsent
        /// addresses stored in the database.
        /// </summary>
        Task EnqueueSendJobAsync(SendBulkEmailInput input);

        /// <summary>
        /// Returns the currently configured Gmail SMTP settings.
        /// The password itself is never returned to the client.
        /// </summary>
        Task<GmailEmailSettingsDto> GetEmailSettingsAsync();

        /// <summary>
        /// Saves Gmail SMTP settings in ABP Setting Manager.
        /// </summary>
        Task UpdateEmailSettingsAsync(UpdateGmailEmailSettingsInput input);
    }
}
