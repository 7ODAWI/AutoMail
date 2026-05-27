using Abp.BackgroundJobs;
using Abp.Domain.Repositories;
using Abp.Timing;
using Abp.UI;
using AutoMail.BulkEmail.Ai;
using AutoMail.BulkEmail.Dto;
using AutoMail.BulkEmail.Jobs;
using AutoMail.Project_Models;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public class EmailOperationAppService : AutoMailAppServiceBase, IEmailOperationAppService
    {
        private const int MaxRetries = 3;
        private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };
                private const string DefaultAiPrompt = @"You are an expert human-style email copywriter and email deliverability specialist.

Your task is to generate multiple unique fundraising and personal outreach emails based on the story below.

The emails must feel authentic, manually written, emotionally real, and optimized to reduce spam detection.

MAIN GOAL:
Generate highly varied email versions from three freelancers in Gaza trying to rebuild their lives and workspace after the war.

ANTI-SPAM & HUMANIZATION RULES:

* Every email must look naturally handwritten by a real person.
* Avoid repetitive wording, structure, and formatting.
* Avoid corporate or marketing tone.
* Avoid excessive emotional exaggeration.
* Avoid spam-trigger phrases such as:

    * ""urgent""
    * ""act now""
    * ""donate immediately""
    * ""click here""
    * ""limited time""
    * excessive punctuation
    * ALL CAPS
* Use clean, natural formatting.
* Keep emails conversational and believable.
* Vary sentence lengths and openings.
* Some emails should feel casual and soft.
* Some should feel more personal and reflective.
* Some should sound like life updates rather than fundraising.

PHOTO VARIATION RULES:

* Use different combinations of attached photos naturally across emails.
* Never mention the exact same photo wording repeatedly.
* Some emails should:

    * include 1 photo
    * include 2 photos
    * include 3 photos
    * include no photos at all
* Mention photos casually and naturally.
* The photos should feel documentary and authentic, not promotional.
* Avoid dramatic phrases like:

    * ""shocking images""
    * ""look at this tragedy""
    * ""exclusive photos""
* Some emails should not mention photos even if photos are attached.
* Rotate emotional focus between:

    * workspace
    * family
    * rebuilding
    * remote work
    * internet/electricity struggles
    * solar setup
    * hope and resilience

EMAIL VARIATION REQUIREMENTS:

* Generate at least 25 completely different emails.
* Every email must have:

    * different opening
    * different flow
    * different sentence structure
    * different emotional emphasis
* Generate:

    * short emails
    * medium emails
    * longer storytelling emails
* Some emails should start with:

    * a personal thought
    * a daily life moment
    * gratitude
    * a small story
    * a calm introduction
    * a simple check-in

DELIVERABILITY OPTIMIZATION:

* Reduce repetitive patterns across all emails.
* Make every email appear manually typed.
* Avoid identical signatures every time.
* Slightly vary sign-offs naturally.
* Keep link placement inconsistent:

    * sometimes middle
    * sometimes end
    * sometimes after the signature
* Some emails should ask mainly for sharing/support instead of donations.

ORIGINAL STORY CONTEXT:

""My brothers and I are freelancers from Gaza who used to work in programming and web development before the war changed our lives completely.

We lost our home, workspace, and income, but we are trying to rebuild with dignity and hope.

Our goal is to create a small safe shelter and a solar-powered workspace so we can continue working online and supporting our family again.""

OUTPUT FORMAT:
For every generated email include:

1. Subject line
2. Email body
3. Suggested photo count
4. Suggested photo type to attach

Examples of photo types:

* workspace photo
* family corner
* laptop setup
* solar battery setup
* daily life moment
* damaged area nearby
* temporary shelter
* internet/work setup

IMPORTANT:
The emails must feel human, personal, calm, and trustworthy - not like mass marketing campaigns.
https://d2g8igdw686xgo.cloudfront.net/98533411_177124836350997_r.jpg
https://d2g8igdw686xgo.cloudfront.net/98533411_1771248496480517_r.jpg
https://d2g8igdw686xgo.cloudfront.net/98533411_1771248495813018_r.jpg
https://d2g8igdw686xgo.cloudfront.net/98533411_1771248495575832_r.jpg
https://images.gofundme.com/0nhw1u4UFekT2EATVPKhFNDN0oI=/720x405/https://d2g8igdw686xgo.cloudfront.net/98533411_1771246676920317_r.png

Campaign links:
https://www.gofundme.com/f/gaza-war-recovery-temporary-shelter-and-livelihood?attribution_id=sl:7a54d6ef-76c0-4270-bd84-a68eeaaa26ba&lang=en_US&ts=1778760655&utm_campaign=fp_below_fold&utm_content=amp20_t1&utm_medium=customer&utm_source=copy_link
https://gofund.me/d016a7efa";

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IRepository<EmailOperation, long> _operationRepository;
        private readonly IRepository<OperationEmail, long> _operationEmailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly IRepository<EmailTemplate, long> _templateRepository;
        private readonly IRepository<AiGenerationRun, long> _aiGenerationRunRepository;
        private readonly IRepository<AiGeneratedTemplateVersion, long> _aiGeneratedTemplateVersionRepository;
        private readonly IAiTemplateGenerationService _aiTemplateGenerationService;

        public EmailOperationAppService(
            IRepository<EmailOperation, long> operationRepository,
            IRepository<OperationEmail, long> operationEmailRepository,
            IRepository<EmailSender, int> senderRepository,
            IBackgroundJobManager backgroundJobManager,
            IRepository<EmailTemplate, long> templateRepository,
            IRepository<AiGenerationRun, long> aiGenerationRunRepository,
            IRepository<AiGeneratedTemplateVersion, long> aiGeneratedTemplateVersionRepository,
            IAiTemplateGenerationService aiTemplateGenerationService)
        {
            _operationRepository = operationRepository;
            _operationEmailRepository = operationEmailRepository;
            _senderRepository = senderRepository;
            _backgroundJobManager = backgroundJobManager;
            _templateRepository = templateRepository;
            _aiGenerationRunRepository = aiGenerationRunRepository;
            _aiGeneratedTemplateVersionRepository = aiGeneratedTemplateVersionRepository;
            _aiTemplateGenerationService = aiTemplateGenerationService;
        }

        // ------------------------------------------------------------------ //
        //  Create Operation (upload + compose + enqueue in one step)
        // ------------------------------------------------------------------ //

        public async Task<OperationListDto> CreateOperationAsync(CreateOperationInput input)
        {
            if (input.File == null || input.File.Length == 0)
                throw new UserFriendlyException("Please select a valid file.");

            var ext = Path.GetExtension(input.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new UserFriendlyException("Only .xlsx and .csv files are supported.");

            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);

            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured. Please add at least one sender before starting an operation.");

            // Parse file
            IReadOnlyList<string> parsedEmails = ext == ".csv"
                ? await ParseCsvAsync(input.File.OpenReadStream(), input.EmailColumnIndex)
                : ParseExcel(input.File.OpenReadStream(), input.EmailColumnIndex);

            var validEmails = parsedEmails
                .Select(e => e?.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrWhiteSpace(e) && EmailRegex.IsMatch(e))
                .Distinct()
                .ToList();

            if (!validEmails.Any())
                throw new UserFriendlyException("No valid email addresses were found in the uploaded file.");

            // Create operation
            var operation = new EmailOperation
            {
                Subject = input.Subject,
                Body = input.Body,
                Status = OperationStatus.Pending,
                TotalEmails = validEmails.Count,
                SentCount = 0,
                FailedCount = 0,
                AiGenerationMode = input.AiGenerationMode,
                AiPrompt = ResolveAiPrompt(input.AiPrompt),
                AiTone = input.AiTone,
                AiVariantCount = input.AiVariantCount,
                AiTemplatesGenerated = false
            };

            operation = await _operationRepository.InsertAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            // Bulk insert operation emails
            foreach (var email in validEmails)
            {
                await _operationEmailRepository.InsertAsync(new OperationEmail
                {
                    OperationId = operation.Id,
                    Email = email,
                    Status = SendStatus.Pending
                });
            }

            await CurrentUnitOfWork.SaveChangesAsync();

            await EnqueueAiGenerationIfNeededAsync(operation);

            // Enqueue background job only when starting immediately
            if (input.StartImmediately)
            {
                await EnsureAiTemplatesReadyBeforeSendAsync(operation);

                await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                    new BulkEmailJobArgs
                    {
                        OperationId = operation.Id
                    });
            }

            return MapToListDto(operation);
        }

        // ------------------------------------------------------------------ //
        //  List Operations
        // ------------------------------------------------------------------ //

        public async Task<List<OperationListDto>> GetAllOperationsAsync()
        {
            var operations = await _operationRepository.GetAll()
                .OrderByDescending(o => o.CreationTime)
                .ToListAsync();

            return operations.Select(MapToListDto).ToList();
        }

        // ------------------------------------------------------------------ //
        //  Operation Detail
        // ------------------------------------------------------------------ //

        public async Task<OperationDetailDto> GetOperationDetailAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            var emails = await _operationEmailRepository.GetAll()
                .Where(e => e.OperationId == operationId)
                .OrderBy(e => e.Id)
                .ToListAsync();

            // Load sender info for sent/failed emails
            var senderIds = emails
                .Where(e => e.SenderId.HasValue)
                .Select(e => e.SenderId.Value)
                .Distinct()
                .ToList();

            var senders = senderIds.Any()
                ? await _senderRepository.GetAll()
                    .Where(s => senderIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Email)
                : new Dictionary<int, string>();

            var templates = await _templateRepository.GetAll()
                .Where(t => t.OperationId == operationId)
                .OrderBy(t => t.Id)
                .ToListAsync();

            var retryableCount = emails.Count(e => e.Status == SendStatus.Failed && e.RetryCount < MaxRetries);

            return new OperationDetailDto
            {
                Id = operation.Id,
                Subject = operation.Subject,
                Body = operation.Body,
                StatusText = operation.Status.ToString(),
                TotalEmails = operation.TotalEmails,
                SentCount = operation.SentCount,
                FailedCount = operation.FailedCount,
                PendingCount = operation.TotalEmails - operation.SentCount - operation.FailedCount,
                RetryableCount = retryableCount,
                CreationTime = operation.CreationTime,
                StartedAt = operation.StartedAt,
                CompletedAt = operation.CompletedAt,
                StopReason = operation.StopReason,
                AiGenerationMode = operation.AiGenerationMode,
                AiVariantCount = operation.AiVariantCount,
                AiPrompt = operation.AiPrompt,
                AiTone = operation.AiTone,
                AiLastGeneratedAt = operation.AiLastGeneratedAt,
                AiTemplatesGenerated = operation.AiTemplatesGenerated,
                Templates = templates.Select(MapToTemplateDto).ToList(),
                Emails = emails.Select(e => new OperationEmailDto
                {
                    Email = e.Email,
                    StatusText = e.Status.ToString(),
                    RetryCount = e.RetryCount,
                    ErrorMessage = e.ErrorMessage,
                    SentAt = e.SentAt,
                    SenderEmail = e.SenderId.HasValue && senders.ContainsKey(e.SenderId.Value)
                        ? senders[e.SenderId.Value]
                        : null
                }).ToList()
            };
        }

        // ------------------------------------------------------------------ //
        //  Retry Failed Emails (scoped to an operation)
        // ------------------------------------------------------------------ //

        public async Task RetryFailedEmailsAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            var failedEmails = await _operationEmailRepository.GetAll()
                .Where(e => e.OperationId == operationId
                         && e.Status == SendStatus.Failed
                         && e.RetryCount < MaxRetries)
                .ToListAsync();

            if (!failedEmails.Any())
                throw new UserFriendlyException("No retryable failed emails found for this operation.");

            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);

            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured.");

            // Reset failed emails back to Pending for re-processing
            foreach (var email in failedEmails)
            {
                email.Status = SendStatus.Pending;
                email.ErrorMessage = null;
                await _operationEmailRepository.UpdateAsync(email);
            }

            // Update operation status
            operation.Status = OperationStatus.Pending;
            operation.CompletedAt = null;
            operation.FailedCount = operation.FailedCount - failedEmails.Count;
            await _operationRepository.UpdateAsync(operation);

            await CurrentUnitOfWork.SaveChangesAsync();

            await EnsureAiTemplatesReadyBeforeSendAsync(operation);

            // Enqueue job
            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs
                {
                    OperationId = operationId
                });
        }

        // ------------------------------------------------------------------ //
        //  Export Failed Emails CSV (scoped to an operation)
        // ------------------------------------------------------------------ //

        public async Task<byte[]> ExportFailedEmailsCsvAsync(long operationId)
        {
            var failedEmails = await _operationEmailRepository.GetAll()
                .Where(e => e.OperationId == operationId && e.Status == SendStatus.Failed)
                .OrderBy(e => e.Id)
                .ToListAsync();

            if (!failedEmails.Any())
                throw new UserFriendlyException("No failed emails to export for this operation.");

            var sb = new StringBuilder();
            sb.AppendLine("Email,ErrorMessage,RetryCount,LastAttemptTime");

            foreach (var email in failedEmails)
            {
                var escapedError = (email.ErrorMessage ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"\"{email.Email}\",\"{escapedError}\",{email.RetryCount},{email.SentAt:yyyy-MM-dd HH:mm:ss}");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        // ------------------------------------------------------------------ //
        //  Export Distinct Emails (Excel)
        // ------------------------------------------------------------------ //
        public async Task<List<string>> GetDistinctEmailsAsync()
        {
            return await _operationEmailRepository.GetAll()
                .Select(e => e.Email)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();
        }
        public async Task<List<string>> GetEmailsAsync()
        {
            return await _operationEmailRepository.GetAll()
                .Select(e => e.Email)
                .OrderBy(e => e)
                .ToListAsync();
        }
        public async Task<byte[]> ExportDistinctEmailsExcelAsync()
        {
            var distinctEmails = await _operationEmailRepository.GetAll()
                .Select(e => e.Email)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            if (!distinctEmails.Any())
                throw new UserFriendlyException("No emails found to export.");

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Emails");

            worksheet.Cell(1, 1).Value = "Email";
            worksheet.Cell(1, 1).Style.Font.Bold = true;

            for (int i = 0; i < distinctEmails.Count; i++)
            {
                worksheet.Cell(i + 2, 1).Value = distinctEmails[i];
            }

            worksheet.Column(1).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ------------------------------------------------------------------ //
        //  Pause Operation
        // ------------------------------------------------------------------ //

        public async Task PauseOperationAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status != OperationStatus.InProgress && operation.Status != OperationStatus.Pending)
                throw new UserFriendlyException("Only pending or in-progress operations can be paused.");

            operation.Status = OperationStatus.Paused;
            operation.StopReason = "Paused by user.";
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ //
        //  Stop Operation (Cancel)
        // ------------------------------------------------------------------ //

        public async Task StopOperationAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status != OperationStatus.InProgress
                && operation.Status != OperationStatus.Paused
                && operation.Status != OperationStatus.Pending)
                throw new UserFriendlyException("Only pending, in-progress or paused operations can be stopped.");

            operation.Status = OperationStatus.Cancelled;
            operation.CompletedAt = Abp.Timing.Clock.Now;
            operation.StopReason = "Stopped by user.";
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ //
        //  Reactivate Operation (resume from Paused or Cancelled)
        // ------------------------------------------------------------------ //

        public async Task ReactivateOperationAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status != OperationStatus.Paused
                && operation.Status != OperationStatus.Cancelled
                && operation.Status != OperationStatus.PartiallySent)
                throw new UserFriendlyException("Only paused, cancelled or partially-sent operations can be reactivated.");

            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);

            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured. Please add at least one sender before reactivating.");

            var hasPendingEmails = await _operationEmailRepository.GetAll()
                .AnyAsync(e => e.OperationId == operationId && e.Status == SendStatus.Pending);

            if (!hasPendingEmails)
                throw new UserFriendlyException("No pending emails remaining in this operation.");

            operation.Status = OperationStatus.Pending;
            operation.CompletedAt = null;
            operation.StopReason = null;
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            await EnsureAiTemplatesReadyBeforeSendAsync(operation);

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs { OperationId = operationId });
        }

        // ------------------------------------------------------------------ //
        //  Complete Send for Unsent Emails (Pending + Failed)
        // ------------------------------------------------------------------ //

        public async Task CompleteUnsentEmailsAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status == OperationStatus.InProgress)
                throw new UserFriendlyException("Operation is already in progress.");

            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);
            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured.");

            var unsentEmails = await _operationEmailRepository.GetAll()
                .Where(e => e.OperationId == operationId && e.Status != SendStatus.Success)
                .ToListAsync();

            if (!unsentEmails.Any())
                throw new UserFriendlyException("All emails were already sent successfully.");

            // Reset all unsent items to Pending so they can be completed in one run.
            foreach (var email in unsentEmails)
            {
                email.Status = SendStatus.Pending;
                email.ErrorMessage = null;
                email.SentAt = null;
                if (email.RetryCount >= MaxRetries)
                {
                    email.RetryCount = 0;
                }

                await _operationEmailRepository.UpdateAsync(email);
            }

            var sentCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == operationId && e.Status == SendStatus.Success);

            operation.Status = OperationStatus.Pending;
            operation.CompletedAt = null;
            operation.StopReason = null;
            operation.SentCount = sentCount;
            operation.FailedCount = 0;
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            await EnsureAiTemplatesReadyBeforeSendAsync(operation);

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs { OperationId = operationId });
        }

        // ------------------------------------------------------------------ //
        //  Start Draft Operation
        // ------------------------------------------------------------------ //

        public async Task StartOperationAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status != OperationStatus.Pending)
                throw new UserFriendlyException("Only pending (draft) operations can be started.");

            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);
            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured.");

            var hasPendingEmails = await _operationEmailRepository.GetAll()
                .AnyAsync(e => e.OperationId == operationId && e.Status == SendStatus.Pending);
            if (!hasPendingEmails)
                throw new UserFriendlyException("No pending emails found in this operation.");

            await EnsureAiTemplatesReadyBeforeSendAsync(operation);

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs { OperationId = operationId });
        }

        public async Task DeleteOperationAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status == OperationStatus.InProgress)
            {
                operation.Status = OperationStatus.Cancelled;
                operation.StopReason = "Force deleted by user.";
                operation.CompletedAt = Clock.Now;
                await _operationRepository.UpdateAsync(operation);
                await CurrentUnitOfWork.SaveChangesAsync();
            }

            var operationEmails = await _operationEmailRepository.GetAll()
                .Where(e => e.OperationId == operationId)
                .ToListAsync();

            foreach (var email in operationEmails)
            {
                await _operationEmailRepository.DeleteAsync(email.Id);
            }

            var templates = await _templateRepository.GetAll()
                .Where(t => t.OperationId == operationId)
                .ToListAsync();

            foreach (var template in templates)
            {
                await _templateRepository.DeleteAsync(template.Id);
            }

            var generatedVersions = await _aiGeneratedTemplateVersionRepository.GetAll()
                .Where(v => v.OperationId == operationId)
                .ToListAsync();

            foreach (var version in generatedVersions)
            {
                await _aiGeneratedTemplateVersionRepository.DeleteAsync(version.Id);
            }

            var generationRuns = await _aiGenerationRunRepository.GetAll()
                .Where(r => r.OperationId == operationId)
                .ToListAsync();

            foreach (var run in generationRuns)
            {
                await _aiGenerationRunRepository.DeleteAsync(run.Id);
            }

            await _operationRepository.DeleteAsync(operation.Id);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ //
        //  Update (Edit) Pending Operation
        // ------------------------------------------------------------------ //

        public async Task UpdateOperationAsync(UpdateOperationInput input)
        {
            var operation = await _operationRepository.GetAsync(input.Id);

            if (operation.Status != OperationStatus.Pending)
                throw new UserFriendlyException("Only pending (draft) operations can be edited.");

            var aiSettingsChanged = operation.AiGenerationMode != input.AiGenerationMode
                || operation.AiVariantCount != input.AiVariantCount
                || !string.Equals(operation.AiPrompt ?? string.Empty, input.AiPrompt ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(operation.AiTone ?? string.Empty, input.AiTone ?? string.Empty, StringComparison.Ordinal);

            operation.Subject = input.Subject?.Trim();
            operation.Body = input.Body;
            operation.AiGenerationMode = input.AiGenerationMode;
            operation.AiVariantCount = input.AiVariantCount;
            operation.AiPrompt = input.AiPrompt;
            operation.AiTone = input.AiTone;

            if (aiSettingsChanged)
            {
                var existingAiTemplates = await _templateRepository.GetAll()
                    .Where(t => t.OperationId == input.Id && t.IsAiGenerated)
                    .ToListAsync();

                foreach (var template in existingAiTemplates)
                {
                    var referencingEmails = await _operationEmailRepository.GetAll()
                        .Where(e => e.TemplateId == template.Id)
                        .ToListAsync();
                    foreach (var email in referencingEmails)
                    {
                        email.TemplateId = null;
                        await _operationEmailRepository.UpdateAsync(email);
                    }

                    await _templateRepository.DeleteAsync(template.Id);
                }

                operation.AiLastGeneratedAt = null;
                operation.AiTemplatesGenerated = false;
            }

            await _operationRepository.UpdateAsync(operation);

            if (input.File != null && input.File.Length > 0)
            {
                var ext = Path.GetExtension(input.File.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    throw new UserFriendlyException("Only .xlsx and .csv files are supported.");

                // Replace existing pending emails
                var existing = await _operationEmailRepository.GetAll()
                    .Where(e => e.OperationId == input.Id)
                    .ToListAsync();
                foreach (var e in existing)
                    await _operationEmailRepository.DeleteAsync(e.Id);

                IReadOnlyList<string> parsed = ext == ".csv"
                    ? await ParseCsvAsync(input.File.OpenReadStream(), input.EmailColumnIndex)
                    : ParseExcel(input.File.OpenReadStream(), input.EmailColumnIndex);

                var validEmails = parsed
                    .Select(e => e?.Trim().ToLowerInvariant())
                    .Where(e => !string.IsNullOrWhiteSpace(e) && EmailRegex.IsMatch(e))
                    .Distinct()
                    .ToList();

                if (!validEmails.Any())
                    throw new UserFriendlyException("No valid email addresses found in the uploaded file.");

                foreach (var email in validEmails)
                    await _operationEmailRepository.InsertAsync(new OperationEmail
                    {
                        OperationId = input.Id,
                        Email = email,
                        Status = SendStatus.Pending
                    });

                operation.TotalEmails = validEmails.Count;
                operation.SentCount = 0;
                operation.FailedCount = 0;
                await _operationRepository.UpdateAsync(operation);
            }

            await CurrentUnitOfWork.SaveChangesAsync();

            await EnqueueAiGenerationIfNeededAsync(operation);
        }

        // ------------------------------------------------------------------ //
        //  Clone Operation (new draft copy)
        // ------------------------------------------------------------------ //

        public async Task<OperationListDto> CloneOperationAsync(long operationId)
        {
            var original = await _operationRepository.GetAsync(operationId);

            var cloned = new EmailOperation
            {
                Subject = original.Subject,
                Body = original.Body,
                Status = OperationStatus.Pending,
                TotalEmails = 0,
                SentCount = 0,
                FailedCount = 0,
                AiGenerationMode = original.AiGenerationMode,
                AiVariantCount = original.AiVariantCount,
                AiPrompt = original.AiPrompt,
                AiTone = original.AiTone,
                AiTemplatesGenerated = false
            };
            cloned = await _operationRepository.InsertAsync(cloned);
            await CurrentUnitOfWork.SaveChangesAsync();

            // Copy all emails, reset to Pending
            var emails = await _operationEmailRepository.GetAll()
                .Where(e => e.OperationId == operationId)
                .ToListAsync();
            foreach (var e in emails)
                await _operationEmailRepository.InsertAsync(new OperationEmail
                {
                    OperationId = cloned.Id,
                    Email = e.Email,
                    Status = SendStatus.Pending
                });

            cloned.TotalEmails = emails.Count;
            await _operationRepository.UpdateAsync(cloned);

            // Copy templates
            var templates = await _templateRepository.GetAll()
                .Where(t => t.OperationId == operationId)
                .ToListAsync();
            foreach (var t in templates)
                await _templateRepository.InsertAsync(new EmailTemplate
                {
                    OperationId = cloned.Id,
                    Name = t.Name,
                    Subject = t.Subject,
                    Body = t.Body,
                    Weight = t.Weight,
                    PreviewText = t.PreviewText,
                    IsAiGenerated = t.IsAiGenerated,
                    SimilarityScore = t.SimilarityScore
                });

            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToListDto(cloned);
        }

        // ------------------------------------------------------------------ //
        //  Template CRUD
        // ------------------------------------------------------------------ //

        public async Task<EmailTemplateDto> AddTemplateAsync(CreateTemplateInput input)
        {
            var operation = await _operationRepository.GetAsync(input.OperationId);
            if (operation.Status == OperationStatus.InProgress)
                throw new UserFriendlyException("Cannot add templates to an in-progress operation.");

            var template = new EmailTemplate
            {
                OperationId = input.OperationId,
                Name = input.Name?.Trim(),
                Subject = input.Subject?.Trim(),
                Body = input.Body,
                Weight = input.Weight,
                PreviewText = input.PreviewText,
                IsAiGenerated = input.IsAiGenerated,
                SimilarityScore = input.SimilarityScore,
                AiGenerationRunId = input.AiGenerationRunId,
                AiGeneratedVersionId = input.AiGeneratedVersionId
            };

            await _templateRepository.InsertAsync(template);
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToTemplateDto(template);
        }

        public async Task<EmailTemplateDto> UpdateTemplateAsync(UpdateTemplateInput input)
        {
            var template = await _templateRepository.GetAsync(input.Id);
            var operation = await _operationRepository.GetAsync(template.OperationId);
            if (operation.Status == OperationStatus.InProgress)
                throw new UserFriendlyException("Cannot edit templates of an in-progress operation.");

            template.Name = input.Name?.Trim();
            template.Subject = input.Subject?.Trim();
            template.Body = input.Body;
            template.Weight = input.Weight;
            template.PreviewText = input.PreviewText;
            template.IsAiGenerated = input.IsAiGenerated;
            template.SimilarityScore = input.SimilarityScore;
            template.AiGenerationRunId = input.AiGenerationRunId;
            template.AiGeneratedVersionId = input.AiGeneratedVersionId;

            await _templateRepository.UpdateAsync(template);
            await CurrentUnitOfWork.SaveChangesAsync();
            return MapToTemplateDto(template);
        }

        public async Task<GenerateAiTemplatesResultDto> GenerateAiTemplatesAsync(GenerateAiTemplatesInput input)
        {
            var operation = await _operationRepository.GetAsync(input.OperationId);
            if (operation.Status == OperationStatus.InProgress)
                throw new UserFriendlyException("Cannot generate AI templates for an in-progress operation.");

            input.Prompt = ResolveAiPrompt(string.IsNullOrWhiteSpace(input.Prompt) ? operation.AiPrompt : input.Prompt);
            if (!string.Equals(operation.AiPrompt, input.Prompt, StringComparison.Ordinal))
            {
                operation.AiPrompt = input.Prompt;
                await _operationRepository.UpdateAsync(operation);
                await CurrentUnitOfWork.SaveChangesAsync();
            }

            var historicalTemplates = await _templateRepository.GetAll()
                .OrderByDescending(t => t.CreationTime)
                .Take(500)
                .ToListAsync();

            var generationRun = new AiGenerationRun
            {
                OperationId = operation.Id,
                Status = AiGenerationRunStatus.Running,
                RequestedVariants = input.VariantCount,
                StartedAt = Clock.Now,
                CorrelationId = Guid.NewGuid().ToString("N"),
                ModelRoute = "balanced"
            };

            generationRun = await _aiGenerationRunRepository.InsertAsync(generationRun);
            await CurrentUnitOfWork.SaveChangesAsync();

            try
            {
                var variants = await _aiTemplateGenerationService.GenerateTemplatesAsync(operation, historicalTemplates, input);
                var generatedTemplates = new List<EmailTemplateDto>();

                foreach (var variant in variants)
                {
                    var version = new AiGeneratedTemplateVersion
                    {
                        OperationId = operation.Id,
                        GenerationRunId = generationRun.Id,
                        Subject = variant.Subject,
                        PreviewText = variant.PreviewText,
                        BodyHtml = variant.BodyHtml,
                        OutlineJson = variant.OutlineJson,
                        ComponentOrderJson = variant.ComponentOrderJson,
                        ModelName = variant.ModelName,
                        PromptVersion = variant.PromptVersion,
                        InputTokens = variant.InputTokens,
                        OutputTokens = variant.OutputTokens,
                        LatencyMs = variant.LatencyMs,
                        SimilarityScore = variant.SimilarityScore,
                        SubjectHash = ComputeSha256(variant.Subject),
                        BodyHash = ComputeSha256(variant.BodyHtml),
                        StructureHash = ComputeSha256($"{variant.OutlineJson}|{variant.ComponentOrderJson}")
                    };

                    version = await _aiGeneratedTemplateVersionRepository.InsertAsync(version);
                    await CurrentUnitOfWork.SaveChangesAsync();

                    var template = new EmailTemplate
                    {
                        OperationId = operation.Id,
                        Name = BuildAiTemplateName(generationRun.Id, generatedTemplates.Count + 1),
                        Subject = variant.Subject,
                        Body = variant.BodyHtml,
                        PreviewText = variant.PreviewText,
                        Weight = variant.Weight <= 0 ? 1 : variant.Weight,
                        IsAiGenerated = true,
                        SimilarityScore = variant.SimilarityScore,
                        AiGenerationRunId = generationRun.Id,
                        AiGeneratedVersionId = version.Id
                    };

                    template = await _templateRepository.InsertAsync(template);
                    generatedTemplates.Add(MapToTemplateDto(template));
                }

                generationRun.Status = AiGenerationRunStatus.Completed;
                generationRun.GeneratedVariants = generatedTemplates.Count;
                generationRun.CompletedAt = Clock.Now;

                operation.AiGenerationMode = AiGenerationMode.PreGeneratedPool;
                operation.AiVariantCount = input.VariantCount;
                operation.AiPrompt = input.Prompt;
                operation.AiTone = input.Tone;
                operation.AiLastGeneratedAt = Clock.Now;
                operation.AiTemplatesGenerated = true;

                await _aiGenerationRunRepository.UpdateAsync(generationRun);
                await _operationRepository.UpdateAsync(operation);
                await CurrentUnitOfWork.SaveChangesAsync();

                return new GenerateAiTemplatesResultDto
                {
                    GenerationRunId = generationRun.Id,
                    Status = generationRun.Status.ToString(),
                    RequestedCount = input.VariantCount,
                    GeneratedCount = generatedTemplates.Count,
                    Templates = generatedTemplates
                };
            }
            catch (Exception ex)
            {
                generationRun.Status = AiGenerationRunStatus.Failed;
                generationRun.ErrorMessage = ex.Message;
                generationRun.CompletedAt = Clock.Now;
                operation.AiTemplatesGenerated = false;
                await _operationRepository.UpdateAsync(operation);
                await _aiGenerationRunRepository.UpdateAsync(generationRun);
                await CurrentUnitOfWork.SaveChangesAsync();
                throw;
            }
        }

        public async Task DeleteTemplateAsync(long templateId)
        {
            var template = await _templateRepository.GetAsync(templateId);
            var operation = await _operationRepository.GetAsync(template.OperationId);
            if (operation.Status == OperationStatus.InProgress)
                throw new UserFriendlyException("Cannot delete templates from an in-progress operation.");

            // Null out TemplateId on any sent emails that referenced this template
            // (FK uses NoAction to avoid SQL Server multiple-cascade-paths error)
            var referencingEmails = await _operationEmailRepository.GetAll()
                .Where(e => e.TemplateId == templateId)
                .ToListAsync();
            foreach (var email in referencingEmails)
            {
                email.TemplateId = null;
                await _operationEmailRepository.UpdateAsync(email);
            }

            await _templateRepository.DeleteAsync(templateId);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ //
        //  Private Helpers
        // ------------------------------------------------------------------ //

        private static OperationListDto MapToListDto(EmailOperation op)
        {
            return new OperationListDto
            {
                Id = op.Id,
                Subject = op.Subject,
                StatusText = op.Status.ToString(),
                TotalEmails = op.TotalEmails,
                SentCount = op.SentCount,
                FailedCount = op.FailedCount,
                PendingCount = op.TotalEmails - op.SentCount - op.FailedCount,
                CreationTime = op.CreationTime,
                StartedAt = op.StartedAt,
                CompletedAt = op.CompletedAt,
                StopReason = op.StopReason,
                AiGenerationMode = op.AiGenerationMode,
                AiVariantCount = op.AiVariantCount,
                AiLastGeneratedAt = op.AiLastGeneratedAt,
                AiTemplatesGenerated = op.AiTemplatesGenerated
            };
        }

        private async Task EnqueueAiGenerationIfNeededAsync(EmailOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            if (operation.AiGenerationMode != AiGenerationMode.PreGeneratedPool || operation.AiVariantCount <= 0)
            {
                return;
            }

            if (operation.AiTemplatesGenerated)
            {
                return;
            }

            await _backgroundJobManager.EnqueueAsync<AiTemplateGenerationJob, AiTemplateGenerationJobArgs>(
                new AiTemplateGenerationJobArgs
                {
                    OperationId = operation.Id
                });
        }

        private async Task EnsureAiTemplatesReadyBeforeSendAsync(EmailOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            if (operation.AiGenerationMode != AiGenerationMode.PreGeneratedPool || operation.AiVariantCount <= 0)
            {
                return;
            }

            var normalizedPrompt = ResolveAiPrompt(operation.AiPrompt);
            if (!string.Equals(operation.AiPrompt, normalizedPrompt, StringComparison.Ordinal))
            {
                operation.AiPrompt = normalizedPrompt;
                await _operationRepository.UpdateAsync(operation);
                await CurrentUnitOfWork.SaveChangesAsync();
            }

            if (operation.AiTemplatesGenerated)
            {
                return;
            }

            await EnqueueAiGenerationIfNeededAsync(operation);
            throw new UserFriendlyException("AI template generation is running in background. Please retry operation start after generation completes.");
        }

        private static EmailTemplateDto MapToTemplateDto(EmailTemplate t) =>
            new EmailTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Subject = t.Subject,
                Body = t.Body,
                PreviewText = t.PreviewText,
                Weight = t.Weight,
                IsAiGenerated = t.IsAiGenerated,
                SimilarityScore = t.SimilarityScore,
                AiGenerationRunId = t.AiGenerationRunId,
                AiGeneratedVersionId = t.AiGeneratedVersionId
            };

        private static string BuildAiTemplateName(long generationRunId, int index)
            => $"AI Variant #{index} (Run {generationRunId})";

        private static string ComputeSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static string ResolveAiPrompt(string prompt)
        {
            var effectivePrompt = string.IsNullOrWhiteSpace(prompt)
                ? DefaultAiPrompt
                : prompt.Trim();

            return effectivePrompt.Length <= EmailOperation.MaxAiPromptLength
                ? effectivePrompt
                : effectivePrompt.Substring(0, EmailOperation.MaxAiPromptLength);
        }

        private static IReadOnlyList<string> ParseExcel(Stream stream, int columnIndex)
        {
            var emails = new List<string>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (int row = 2; row <= lastRow; row++)
            {
                var cell = worksheet.Cell(row, columnIndex + 1);
                var value = cell.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    emails.Add(value);
            }

            return emails;
        }

        private static async Task<IReadOnlyList<string>> ParseCsvAsync(Stream stream, int columnIndex)
        {
            var emails = new List<string>();

            using var reader = new StreamReader(stream);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                TrimOptions = TrimOptions.Trim
            };

            using var csv = new CsvReader(reader, config);

            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                var value = csv.GetField(columnIndex)?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    emails.Add(value);
            }

            return emails;
        }
    }
}
