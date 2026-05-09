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
    }
}
