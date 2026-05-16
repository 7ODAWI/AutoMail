using Abp.Application.Services;
using AutoMail.BulkEmail.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public interface IEmailOperationAppService : IApplicationService
    {
        Task<OperationListDto> CreateOperationAsync(CreateOperationInput input);
        Task<List<OperationListDto>> GetAllOperationsAsync();
        Task<OperationDetailDto> GetOperationDetailAsync(long operationId);
        Task RetryFailedEmailsAsync(long operationId);
        Task<byte[]> ExportFailedEmailsCsvAsync(long operationId);
        Task<byte[]> ExportDistinctEmailsExcelAsync();
        Task<List<string>> GetDistinctEmailsAsync();
        Task<List<string>> GetEmailsAsync();

        // Operation control
        Task PauseOperationAsync(long operationId);
        Task StopOperationAsync(long operationId);
        Task ReactivateOperationAsync(long operationId);
        Task CompleteUnsentEmailsAsync(long operationId);
        Task StartOperationAsync(long operationId);

        // Draft management
        Task UpdateOperationAsync(UpdateOperationInput input);
        Task<OperationListDto> CloneOperationAsync(long operationId);

        // Template management
        Task<EmailTemplateDto> AddTemplateAsync(CreateTemplateInput input);
        Task<EmailTemplateDto> UpdateTemplateAsync(UpdateTemplateInput input);
        Task DeleteTemplateAsync(long templateId);
    }
}
