using Abp.BackgroundJobs;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AutoMail.BulkEmail.Dto;
using AutoMail.Project_Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Jobs
{
    public class AiTemplateGenerationJob : AsyncBackgroundJob<AiTemplateGenerationJobArgs>, ITransientDependency
    {
        private readonly IRepository<EmailOperation, long> _operationRepository;
        private readonly IRepository<AiGenerationRun, long> _generationRunRepository;
        private readonly IEmailOperationAppService _emailOperationAppService;

        public AiTemplateGenerationJob(
            IRepository<EmailOperation, long> operationRepository,
            IRepository<AiGenerationRun, long> generationRunRepository,
            IEmailOperationAppService emailOperationAppService)
        {
            _operationRepository = operationRepository;
            _generationRunRepository = generationRunRepository;
            _emailOperationAppService = emailOperationAppService;
        }

        [UnitOfWork]
        public override async Task ExecuteAsync(AiTemplateGenerationJobArgs args)
        {
            var operation = await _operationRepository.GetAsync(args.OperationId);

            if (operation.AiGenerationMode != AiGenerationMode.PreGeneratedPool || operation.AiVariantCount <= 0)
            {
                return;
            }

            if (operation.AiTemplatesGenerated)
            {
                return;
            }

            var hasRunningGeneration = await _generationRunRepository.GetAll()
                .AnyAsync(r => r.OperationId == operation.Id && r.Status == AiGenerationRunStatus.Running);

            if (hasRunningGeneration)
            {
                return;
            }

            try
            {
                await _emailOperationAppService.GenerateAiTemplatesAsync(new GenerateAiTemplatesInput
                {
                    OperationId = operation.Id,
                    VariantCount = operation.AiVariantCount,
                    Prompt = operation.AiPrompt,
                    Tone = operation.AiTone
                });
            }
            catch (Exception ex)
            {
                Logger.Error($"[AiTemplateGenerationJob] AI generation failed for operation {operation.Id}: {ex.Message}", ex);
                throw;
            }
        }
    }
}