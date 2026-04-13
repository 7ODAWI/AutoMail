using Abp.Application.Services;
using AutoMail.BulkEmail.Dto;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public interface IBulkEmailAppService : IApplicationService
    {
        Task<UploadEmailsResult> UploadAndStoreEmailsAsync(UploadEmailsInput input);
        Task EnqueueSendJobAsync(SendBulkEmailInput input);
        Task<byte[]> ExportFailedEmailsCsvAsync();
        Task RetryFailedEmailsAsync(SendBulkEmailInput input);
        Task<BulkEmailDashboardDto> GetDashboardStatsAsync();
    }
}
