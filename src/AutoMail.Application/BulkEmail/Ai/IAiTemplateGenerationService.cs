using AutoMail.BulkEmail.Dto;
using AutoMail.Project_Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Ai
{
    public interface IAiTemplateGenerationService
    {
        Task<List<AiTemplateVariantDto>> GenerateTemplatesAsync(
            EmailOperation operation,
            List<EmailTemplate> historicalTemplates,
            GenerateAiTemplatesInput input,
            CancellationToken cancellationToken = default);
    }
}