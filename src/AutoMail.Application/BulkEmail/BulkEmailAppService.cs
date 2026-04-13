using Abp.BackgroundJobs;
using Abp.Domain.Repositories;
using Abp.Timing;
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
    public class BulkEmailAppService : AutoMailAppServiceBase, IBulkEmailAppService
    {
        private const int MaxRetries = 3;
        private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IRepository<Project_Models.BulkEmail, long> _emailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IRepository<BulkEmailLog, long> _logRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;

        public BulkEmailAppService(
            IRepository<Project_Models.BulkEmail, long> emailRepository,
            IRepository<EmailSender, int> senderRepository,
            IRepository<BulkEmailLog, long> logRepository,
            IBackgroundJobManager backgroundJobManager)
        {
            _emailRepository = emailRepository;
            _senderRepository = senderRepository;
            _logRepository = logRepository;
            _backgroundJobManager = backgroundJobManager;
        }

        // ------------------------------------------------------------------ //
        //  Upload & Store
        // ------------------------------------------------------------------ //

        public async Task<UploadEmailsResult> UploadAndStoreEmailsAsync(UploadEmailsInput input)
        {
            if (input.File == null || input.File.Length == 0)
                throw new UserFriendlyException("Please select a valid file.");

            var ext = Path.GetExtension(input.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new UserFriendlyException("Only .xlsx and .csv files are supported.");

            IReadOnlyList<string> parsedEmails = ext == ".csv"
                ? await ParseCsvAsync(input.File.OpenReadStream(), input.EmailColumnIndex)
                : ParseExcel(input.File.OpenReadStream(), input.EmailColumnIndex);

            var result = new UploadEmailsResult { ParsedCount = parsedEmails.Count };

            var validEmails = parsedEmails
                .Select(e => e?.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrWhiteSpace(e) && EmailRegex.IsMatch(e))
                .Distinct()
                .ToList();

            result.SkippedCount = result.ParsedCount - validEmails.Count;

            if (!validEmails.Any())
                throw new UserFriendlyException("No valid email addresses were found in the uploaded file.");

            var existingEmails = await _emailRepository
                .GetAll()
                .Where(e => validEmails.Contains(e.Email))
                .Select(e => e.Email)
                .ToListAsync();

            var newEmails = validEmails
                .Except(existingEmails, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.SkippedCount += existingEmails.Count;
            result.SavedCount = newEmails.Count;

            foreach (var email in newEmails)
            {
                await _emailRepository.InsertAsync(new Project_Models.BulkEmail
                {
                    Email = email,
                    IsSent = false
                });
            }

            return result;
        }

        // ------------------------------------------------------------------ //
        //  Enqueue Background Job
        // ------------------------------------------------------------------ //

        public async Task EnqueueSendJobAsync(SendBulkEmailInput input)
        {
            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);

            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured. Please add at least one sender before sending.");

            var pendingCount = await _emailRepository.GetAll()
                .CountAsync(e => !e.IsSent);

            if (pendingCount == 0)
                throw new UserFriendlyException("There are no pending emails to send. Please upload a file first.");

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs
                {
                    Subject = input.Subject,
                    Body = input.Body,
                    IsRetry = false
                });
        }

        // ------------------------------------------------------------------ //
        //  Failed Emails — Export & Retry
        // ------------------------------------------------------------------ //

        public async Task<byte[]> ExportFailedEmailsCsvAsync()
        {
            // Get the latest failed attempt per BulkEmail (group by BulkEmailId, take max AttemptNumber)
            var failedLogs = await _logRepository.GetAll()
                .Where(l => l.Status == SendStatus.Failed)
                .GroupBy(l => l.BulkEmailId)
                .Select(g => g.OrderByDescending(l => l.AttemptNumber).First())
                .ToListAsync();

            if (!failedLogs.Any())
                throw new UserFriendlyException("No failed emails to export.");

            var sb = new StringBuilder();
            sb.AppendLine("Email,ErrorMessage,AttemptCount,LastAttemptTime");

            foreach (var log in failedLogs)
            {
                var escapedError = (log.ErrorMessage ?? "").Replace("\"", "\"\"");
                sb.AppendLine($"\"{log.EmailAddress}\",\"{escapedError}\",{log.AttemptNumber},{log.SentTime:yyyy-MM-dd HH:mm:ss}");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public async Task RetryFailedEmailsAsync(SendBulkEmailInput input)
        {
            var hasRetryable = await _emailRepository.GetAll()
                .AnyAsync(e => !e.IsSent && e.RetryCount > 0 && e.RetryCount < MaxRetries);

            if (!hasRetryable)
                throw new UserFriendlyException("No retryable failed emails found. Emails may have exceeded the maximum retry count.");

            var hasActiveSenders = await _senderRepository.GetAll()
                .AnyAsync(s => s.IsActive);

            if (!hasActiveSenders)
                throw new UserFriendlyException("No active email senders configured.");

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs
                {
                    Subject = input.Subject,
                    Body = input.Body,
                    IsRetry = true
                });
        }

        // ------------------------------------------------------------------ //
        //  Dashboard / Monitoring
        // ------------------------------------------------------------------ //

        public async Task<BulkEmailDashboardDto> GetDashboardStatsAsync()
        {
            var todayUtc = Clock.Now.Date;

            var totalPending = await _emailRepository.GetAll()
                .CountAsync(e => !e.IsSent && e.RetryCount < MaxRetries);

            var totalPermanentlyFailed = await _emailRepository.GetAll()
                .CountAsync(e => !e.IsSent && e.RetryCount >= MaxRetries);

            var totalSentToday = await _logRepository.GetAll()
                .CountAsync(l => l.Status == SendStatus.Success
                              && l.SentTime.HasValue
                              && l.SentTime.Value >= todayUtc);

            var totalSentAllTime = await _logRepository.GetAll()
                .CountAsync(l => l.Status == SendStatus.Success);

            var totalFailedToday = await _logRepository.GetAll()
                .CountAsync(l => l.Status == SendStatus.Failed
                              && l.CreationTime >= todayUtc);

            // Per-sender breakdown
            var senders = await _senderRepository.GetAll().ToListAsync();
            var senderBreakdown = new List<SenderStatsDto>();

            foreach (var sender in senders)
            {
                var sentToday = await _logRepository.GetAll()
                    .CountAsync(l => l.SenderId == sender.Id
                                  && l.Status == SendStatus.Success
                                  && l.SentTime.HasValue
                                  && l.SentTime.Value >= todayUtc);

                senderBreakdown.Add(new SenderStatsDto
                {
                    SenderId = sender.Id,
                    DisplayName = sender.DisplayName,
                    Email = sender.Email,
                    SentToday = sentToday,
                    RemainingQuota = Math.Max(0, sender.DailyLimit - sentToday),
                    DailyLimit = sender.DailyLimit,
                    IsActive = sender.IsActive
                });
            }

            return new BulkEmailDashboardDto
            {
                TotalPending = totalPending,
                TotalSentToday = totalSentToday,
                TotalSentAllTime = totalSentAllTime,
                TotalFailedToday = totalFailedToday,
                TotalPermanentlyFailed = totalPermanentlyFailed,
                SenderBreakdown = senderBreakdown
            };
        }

        // ------------------------------------------------------------------ //
        //  Private Parsers
        // ------------------------------------------------------------------ //

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
