using Abp.BackgroundJobs;
using Abp.Domain.Repositories;
using Abp.UI;
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public class EmailOperationAppService : AutoMailAppServiceBase, IEmailOperationAppService
    {
        private const int MaxRetries = 3;
        private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IRepository<EmailOperation, long> _operationRepository;
        private readonly IRepository<OperationEmail, long> _operationEmailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;

        public EmailOperationAppService(
            IRepository<EmailOperation, long> operationRepository,
            IRepository<OperationEmail, long> operationEmailRepository,
            IRepository<EmailSender, int> senderRepository,
            IBackgroundJobManager backgroundJobManager)
        {
            _operationRepository = operationRepository;
            _operationEmailRepository = operationEmailRepository;
            _senderRepository = senderRepository;
            _backgroundJobManager = backgroundJobManager;
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
                FailedCount = 0
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

            // Enqueue background job
            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs
                {
                    OperationId = operation.Id
                });

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

            if (operation.Status != OperationStatus.InProgress)
                throw new UserFriendlyException("Only in-progress operations can be paused.");

            operation.Status = OperationStatus.Paused;
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();
        }

        // ------------------------------------------------------------------ //
        //  Stop Operation (Cancel)
        // ------------------------------------------------------------------ //

        public async Task StopOperationAsync(long operationId)
        {
            var operation = await _operationRepository.GetAsync(operationId);

            if (operation.Status != OperationStatus.InProgress && operation.Status != OperationStatus.Paused)
                throw new UserFriendlyException("Only in-progress or paused operations can be stopped.");

            operation.Status = OperationStatus.Cancelled;
            operation.CompletedAt = Abp.Timing.Clock.Now;
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

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs { OperationId = operationId });
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
                StopReason = op.StopReason
            };
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
