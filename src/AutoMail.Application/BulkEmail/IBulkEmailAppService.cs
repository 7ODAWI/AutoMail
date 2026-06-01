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
        Task<OperationDetailDto> GetOperationOverviewAsync(long operationId);
        Task<OperationPagedResultDto<EmailTemplateDto>> GetOperationTemplatesPagedAsync(long operationId, int pageNumber, int pageSize);
        Task<OperationPagedResultDto<OperationEmailDto>> GetOperationEmailsPagedAsync(long operationId, int pageNumber, int pageSize);
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
        Task DeleteOperationAsync(long operationId);

        // Draft management
        Task UpdateOperationAsync(UpdateOperationInput input);
        Task<OperationListDto> CloneOperationAsync(long operationId);

        // Template management
        Task<EmailTemplateDto> AddTemplateAsync(CreateTemplateInput input);
        Task<EmailTemplateDto> UpdateTemplateAsync(UpdateTemplateInput input);
        Task DeleteTemplateAsync(long templateId);
        Task<GenerateAiTemplatesResultDto> GenerateAiTemplatesAsync(GenerateAiTemplatesInput input);
        Task StopAiGenerationAsync(long operationId);

        // Shared global template pool (standalone AI generation, not tied to any operation)
        Task<GenerateAiTemplatesResultDto> GenerateGlobalAiTemplatesAsync(GenerateGlobalAiTemplatesInput input);
        Task<List<EmailTemplateDto>> GetSharedTemplatesAsync(int pageNumber = 1, int pageSize = 50);
        Task DeleteSharedTemplateAsync(long templateId);
        Task<List<AiGenerationRunDto>> GetGlobalGenerationRunsAsync();
        Task StopGlobalAiGenerationAsync();
    }
}
