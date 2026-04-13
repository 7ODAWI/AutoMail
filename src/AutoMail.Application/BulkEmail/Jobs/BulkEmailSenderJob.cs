using Abp.BackgroundJobs;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Timing;
using AutoMail.Project_Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BulkEmailEntity = AutoMail.Project_Models.BulkEmail;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace AutoMail.BulkEmail.Jobs
{
    public class BulkEmailSenderJob : AsyncBackgroundJob<BulkEmailJobArgs>, ITransientDependency
    {
        private const int MaxRetries = 3;
        private const int PageSize = 200;
        private const int BaseBackoffMs = 2000;
        private const int MaxBackoffMs = 30000;

        private readonly IRepository<BulkEmailEntity, long> _emailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IRepository<BulkEmailLog, long> _logRepository;
        private readonly IMailKitEmailDispatcher _dispatcher;

        public BulkEmailSenderJob(
            IRepository<BulkEmailEntity, long> emailRepository,
            IRepository<EmailSender, int> senderRepository,
            IRepository<BulkEmailLog, long> logRepository,
            IMailKitEmailDispatcher dispatcher)
        {
            _emailRepository = emailRepository;
            _senderRepository = senderRepository;
            _logRepository = logRepository;
            _dispatcher = dispatcher;
        }

        [UnitOfWork]
        public override async Task ExecuteAsync(BulkEmailJobArgs args)
        {
            // ── 1. Load active senders and compute remaining daily quotas ──
            var senders = await _senderRepository.GetAll()
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!senders.Any())
                throw new InvalidOperationException("No active email senders configured. Add at least one active sender before sending.");

            var todayUtc = Clock.Now.Date;
            var activeSenders = new List<SenderQuota>();

            foreach (var sender in senders)
            {
                var sentToday = await _logRepository.GetAll()
                    .CountAsync(l => l.SenderId == sender.Id
                                  && l.Status == SendStatus.Success
                                  && l.SentTime.HasValue
                                  && l.SentTime.Value >= todayUtc);

                var remaining = Math.Max(0, sender.DailyLimit - sentToday);
                if (remaining == 0)
                {
                    Logger.Info($"[BulkEmailSenderJob] Sender '{sender.Email}' already at daily limit ({sender.DailyLimit}). Skipping.");
                    continue;
                }

                SmtpClient client;
                try
                {
                    client = await _dispatcher.ConnectAsync(sender);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[BulkEmailSenderJob] Failed to connect sender '{sender.Email}': {ex.Message}", ex);
                    continue;
                }

                activeSenders.Add(new SenderQuota
                {
                    Sender = sender,
                    Remaining = remaining,
                    Client = client
                });
            }

            if (!activeSenders.Any())
            {
                Logger.Warn("[BulkEmailSenderJob] No senders available (all at daily limit or failed to connect). Aborting.");
                return;
            }

            Logger.Info($"[BulkEmailSenderJob] {activeSenders.Count} sender(s) ready. Subject: '{args.Subject}', IsRetry: {args.IsRetry}");

            // ── 2. Process emails ──
            var roundRobinIndex = 0;
            var totalSent = 0;
            var totalFailed = 0;
            var allSendersExhausted = false;

            try
            {
                var page = 0;
                while (true)
                {
                    var emails = await LoadEmailPageAsync(args.IsRetry, page, PageSize);
                    if (!emails.Any())
                        break;

                    foreach (var email in emails)
                    {
                        // ── Find next available sender (round-robin) ──
                        SenderQuota chosen = null;
                        for (var i = 0; i < activeSenders.Count; i++)
                        {
                            var idx = (roundRobinIndex + i) % activeSenders.Count;
                            if (activeSenders[idx].Remaining > 0)
                            {
                                chosen = activeSenders[idx];
                                roundRobinIndex = (idx + 1) % activeSenders.Count;
                                break;
                            }
                        }

                        if (chosen == null)
                        {
                            Logger.Warn("[BulkEmailSenderJob] All senders exhausted daily limits. Stopping.");
                            allSendersExhausted = true;
                            break;
                        }

                        // ── Exponential backoff for retries ──
                        if (args.IsRetry && email.RetryCount > 0)
                        {
                            var backoffMs = Math.Min(BaseBackoffMs * (1 << (email.RetryCount - 1)), MaxBackoffMs);
                            await Task.Delay(backoffMs);
                        }

                        // ── Send ──
                        var result = await _dispatcher.SendAsync(chosen.Client, chosen.Sender, email.Email, args.Subject, args.Body);

                        // ── Create immutable log record ──
                        await _logRepository.InsertAsync(new BulkEmailLog
                        {
                            BulkEmailId = email.Id,
                            EmailAddress = email.Email,
                            SenderId = chosen.Sender.Id,
                            Status = result.Success ? SendStatus.Success : SendStatus.Failed,
                            ErrorMessage = result.ErrorMessage,
                            SentTime = Clock.Now,
                            AttemptNumber = email.RetryCount + 1
                        });

                        // ── Update email record ──
                        if (result.Success)
                        {
                            email.IsSent = true;
                            totalSent++;
                            Logger.Debug($"[BulkEmailSenderJob] Sent to {email.Email} via {chosen.Sender.Email}");
                        }
                        else
                        {
                            email.RetryCount++;
                            totalFailed++;
                            Logger.Warn($"[BulkEmailSenderJob] Failed {email.Email} via {chosen.Sender.Email}: {result.ErrorMessage}");
                        }

                        await _emailRepository.UpdateAsync(email);

                        // ── Quota management ──
                        chosen.Remaining--;
                        if (chosen.Remaining <= 0)
                        {
                            Logger.Info($"[BulkEmailSenderJob] Sender '{chosen.Sender.Email}' reached daily limit. Disconnecting.");
                            await DisconnectSafely(chosen.Client);
                            activeSenders.Remove(chosen);

                            if (!activeSenders.Any())
                            {
                                Logger.Warn("[BulkEmailSenderJob] All senders exhausted. Stopping.");
                                allSendersExhausted = true;
                                break;
                            }

                            roundRobinIndex %= activeSenders.Count;
                        }

                        // ── Per-sender delay ──
                        if (chosen.Sender.DelayBetweenEmailsMs > 0)
                        {
                            await Task.Delay(chosen.Sender.DelayBetweenEmailsMs);
                        }
                    }

                    if (allSendersExhausted)
                        break;

                    page++;
                }
            }
            finally
            {
                // ── Cleanup: disconnect all remaining SmtpClients ──
                foreach (var sq in activeSenders)
                {
                    await DisconnectSafely(sq.Client);
                }
            }

            Logger.Info($"[BulkEmailSenderJob] Completed. Sent: {totalSent}, Failed: {totalFailed}.");
        }

        private async Task<List<BulkEmailEntity>> LoadEmailPageAsync(bool isRetry, int page, int pageSize)
        {
            var query = _emailRepository.GetAll().Where(e => !e.IsSent);

            if (isRetry)
            {
                query = query.Where(e => e.RetryCount > 0 && e.RetryCount < MaxRetries);
            }

            return await query
                .OrderBy(e => e.Id)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        private static async Task DisconnectSafely(SmtpClient client)
        {
            try
            {
                if (client.IsConnected)
                    await client.DisconnectAsync(true);

                client.Dispose();
            }
            catch
            {
                // Best-effort disconnect
            }
        }

        private class SenderQuota
        {
            public EmailSender Sender { get; set; }
            public int Remaining { get; set; }
            public SmtpClient Client { get; set; }
        }
    }
}
