using Abp.BackgroundJobs;
using Abp.Configuration;
using Abp.Domain.Repositories;
using Abp.Net.Mail;
using Abp.UI;
using AutoMail.BulkEmail.Dto;
using AutoMail.BulkEmail.Jobs;
using AutoMail.Configuration;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    /// <summary>
    /// Handles file upload, email parsing, persistence and job enqueueing.
    /// </summary>
    public class BulkEmailAppService : AutoMailAppServiceBase, IBulkEmailAppService
    {
        // Supported file extensions
        private static readonly string[] AllowedExtensions = { ".xlsx", ".csv" };

        // Simple but robust RFC-compliant email regex
        private static readonly Regex EmailRegex = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IRepository<Project_Models.BulkEmail, long> _emailRepository;
        private readonly IBackgroundJobManager _backgroundJobManager;
        private readonly ISettingManager _settingManager;

        public BulkEmailAppService(
            IRepository<Project_Models.BulkEmail, long> emailRepository,
            IBackgroundJobManager backgroundJobManager,
            ISettingManager settingManager)
        {
            _emailRepository = emailRepository;
            _backgroundJobManager = backgroundJobManager;
            _settingManager = settingManager;
        }

        // ------------------------------------------------------------------ //
        //  Upload & Store
        // ------------------------------------------------------------------ //

        public async Task<UploadEmailsResult> UploadAndStoreEmailsAsync(UploadEmailsInput input)
        {
            if (input.File == null || input.File.Length == 0)
                throw new Abp.UI.UserFriendlyException("Please select a valid file.");

            var ext = Path.GetExtension(input.File.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                throw new Abp.UI.UserFriendlyException("Only .xlsx and .csv files are supported.");

            // --- Parse raw emails from file ---
            IReadOnlyList<string> parsedEmails = ext == ".csv"
                ? await ParseCsvAsync(input.File.OpenReadStream(), input.EmailColumnIndex)
                : ParseExcel(input.File.OpenReadStream(), input.EmailColumnIndex);

            var result = new UploadEmailsResult { ParsedCount = parsedEmails.Count };

            // --- Validate & de-duplicate within the file ---
            var validEmails = parsedEmails
                .Select(e => e?.Trim().ToLowerInvariant())
                .Where(e => !string.IsNullOrWhiteSpace(e) && EmailRegex.IsMatch(e))
                .Distinct()
                .ToList();

            result.SkippedCount = result.ParsedCount - validEmails.Count;

            if (!validEmails.Any())
                throw new Abp.UI.UserFriendlyException("No valid email addresses were found in the uploaded file.");

            // --- Filter out emails already in the database ---
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

            // --- Persist new emails (batch insert) ---
            foreach (var email in newEmails)
            {
                await _emailRepository.InsertAsync(new Project_Models.BulkEmail
                {
                    Email = email,
                    IsSent = false
                });
            }

            // CurrentUnitOfWork is committed automatically by ABP when the
            // application service method completes successfully.

            return result;
        }

        // ------------------------------------------------------------------ //
        //  Enqueue Background Job
        // ------------------------------------------------------------------ //

        public async Task EnqueueSendJobAsync(SendBulkEmailInput input)
        {
            await EnsureEmailSettingsConfiguredAsync();

            var pendingCount = await _emailRepository
                .GetAll()
                .CountAsync(e => !e.IsSent);

            if (pendingCount == 0)
                throw new UserFriendlyException("There are no pending emails to send. Please upload a file first.");

            await _backgroundJobManager.EnqueueAsync<BulkEmailSenderJob, BulkEmailJobArgs>(
                new BulkEmailJobArgs
                {
                    Subject = input.Subject,
                    Body = input.Body
                });
        }

        public async Task<GmailEmailSettingsDto> GetEmailSettingsAsync()
        {
            var userName = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.Smtp.UserName);
            var defaultFromAddress = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.DefaultFromAddress);
            var defaultFromDisplayName = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.DefaultFromDisplayName);
            var password = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.Smtp.Password);

            return new GmailEmailSettingsDto
            {
                UserName = userName,
                DefaultFromAddress = defaultFromAddress,
                DefaultFromDisplayName = defaultFromDisplayName,
                Host = GmailSmtpDefaults.Host,
                Port = GmailSmtpDefaults.Port,
                EnableSsl = GmailSmtpDefaults.EnableSsl,
                HasPassword = !string.IsNullOrWhiteSpace(password)
            };
        }

        public async Task UpdateEmailSettingsAsync(UpdateGmailEmailSettingsInput input)
        {
            if (!EmailRegex.IsMatch(input.UserName))
                throw new UserFriendlyException("Please enter a valid Gmail address.");

            if (!EmailRegex.IsMatch(input.DefaultFromAddress))
                throw new UserFriendlyException("Please enter a valid default from address.");

            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.Host, GmailSmtpDefaults.Host);
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.Port, GmailSmtpDefaults.Port.ToString());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.EnableSsl, GmailSmtpDefaults.EnableSsl.ToString().ToLowerInvariant());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.UseDefaultCredentials, false.ToString().ToLowerInvariant());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.UserName, input.UserName.Trim());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.DefaultFromAddress, input.DefaultFromAddress.Trim());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.DefaultFromDisplayName, input.DefaultFromDisplayName.Trim());

            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.Password, input.Password.Trim());
            }
        }

        // ------------------------------------------------------------------ //
        //  Private Parsers
        // ------------------------------------------------------------------ //

        private static IReadOnlyList<string> ParseExcel(Stream stream, int columnIndex)
        {
            var emails = new List<string>();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.First();

            // Skip the header row (row 1) and read from row 2 onwards
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (int row = 2; row <= lastRow; row++)
            {
                var cell = worksheet.Cell(row, columnIndex + 1); // ClosedXML is 1-based
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

            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();

            while (await csv.ReadAsync())
            {
                // Try to get by index (column-index based approach so it's file-agnostic)
                var value = csv.GetField(columnIndex)?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    emails.Add(value);
            }

            return emails;
        }

        private async Task EnsureEmailSettingsConfiguredAsync()
        {
            var settings = await GetEmailSettingsAsync();

            if (string.IsNullOrWhiteSpace(settings.UserName) ||
                string.IsNullOrWhiteSpace(settings.DefaultFromAddress) ||
                string.IsNullOrWhiteSpace(settings.DefaultFromDisplayName) ||
                !settings.HasPassword)
            {
                throw new UserFriendlyException("Email settings are not configured. Please save the Gmail SMTP settings before sending emails.");
            }
        }
    }
}
