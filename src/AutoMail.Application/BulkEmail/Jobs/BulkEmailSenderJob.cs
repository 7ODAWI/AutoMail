using Abp.BackgroundJobs;
using Abp.Configuration;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Net.Mail;
using AutoMail.Configuration;
using BulkEmailEntity = AutoMail.Project_Models.BulkEmail;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Jobs
{
    /// <summary>
    /// ABP background job that iterates over all unsent <see cref="BulkEmailEntity"/>
    /// records and delivers the email, then marks each record as sent.
    /// </summary>
    public class BulkEmailSenderJob : AsyncBackgroundJob<BulkEmailJobArgs>, ITransientDependency
    {
        private readonly IRepository<BulkEmailEntity, long> _emailRepository;
        private readonly IEmailSender _emailSender;
        private readonly ISettingManager _settingManager;

        public BulkEmailSenderJob(
            IRepository<BulkEmailEntity, long> emailRepository,
            IEmailSender emailSender,
            ISettingManager settingManager)
        {
            _emailRepository = emailRepository;
            _emailSender = emailSender;
            _settingManager = settingManager;
        }

        public override async Task ExecuteAsync(BulkEmailJobArgs args)
        {
            var smtpUserName = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.Smtp.UserName);
            var defaultFromAddress = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.DefaultFromAddress);
            var defaultFromDisplayName = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.DefaultFromDisplayName);
            var smtpPassword = await _settingManager.GetSettingValueForApplicationAsync(EmailSettingNames.Smtp.Password);

            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.Host, GmailSmtpDefaults.Host);
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.Port, GmailSmtpDefaults.Port.ToString());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.EnableSsl, GmailSmtpDefaults.EnableSsl.ToString().ToLowerInvariant());
            await _settingManager.ChangeSettingForApplicationAsync(EmailSettingNames.Smtp.UseDefaultCredentials, false.ToString().ToLowerInvariant());

            if (string.IsNullOrWhiteSpace(smtpUserName) ||
                string.IsNullOrWhiteSpace(defaultFromAddress) ||
                string.IsNullOrWhiteSpace(defaultFromDisplayName) ||
                string.IsNullOrWhiteSpace(smtpPassword))
            {
                throw new InvalidOperationException("Gmail SMTP settings are incomplete. Configure UserName, App Password, DefaultFromAddress and DefaultFromDisplayName before sending.");
            }

            var unsentEmails = await _emailRepository
                .GetAll()
                .Where(e => !e.IsSent)
                .ToListAsync();

            Logger.Info($"[BulkEmailSenderJob] Starting to process {unsentEmails.Count} unsent email(s). SMTP: {GmailSmtpDefaults.Host}:{GmailSmtpDefaults.Port}, User: {smtpUserName}, From: {defaultFromDisplayName} <{defaultFromAddress}>, Subject: '{args.Subject}'");

            foreach (var record in unsentEmails)
            {
                try
                {
                    await _emailSender.SendAsync(
                        to: record.Email,
                        subject: args.Subject,
                        body: args.Body,
                        isBodyHtml: true
                    );

                    record.IsSent = true;
                    await _emailRepository.UpdateAsync(record);

                    Logger.Debug($"[BulkEmailSenderJob] Sent to: {record.Email}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"[BulkEmailSenderJob] Failed to send to {record.Email}: {ex.Message}", ex);
                }
            }

            Logger.Info($"[BulkEmailSenderJob] Completed. Processed {unsentEmails.Count} email(s).");
        }
    }
}
