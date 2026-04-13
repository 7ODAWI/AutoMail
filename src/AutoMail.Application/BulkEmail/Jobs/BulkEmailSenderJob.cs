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
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace AutoMail.BulkEmail.Jobs
{
    public class BulkEmailSenderJob : AsyncBackgroundJob<BulkEmailJobArgs>, ITransientDependency
    {
        private const int MaxRetries = 3;
        private const int PageSize = 200;
        private const int BaseBackoffMs = 2000;
        private const int MaxBackoffMs = 30000;

        private readonly IRepository<EmailOperation, long> _operationRepository;
        private readonly IRepository<OperationEmail, long> _operationEmailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IMailKitEmailDispatcher _dispatcher;

        public BulkEmailSenderJob(
            IRepository<EmailOperation, long> operationRepository,
            IRepository<OperationEmail, long> operationEmailRepository,
            IRepository<EmailSender, int> senderRepository,
            IMailKitEmailDispatcher dispatcher)
        {
            _operationRepository = operationRepository;
            _operationEmailRepository = operationEmailRepository;
            _senderRepository = senderRepository;
            _dispatcher = dispatcher;
        }

        [UnitOfWork]
        public override async Task ExecuteAsync(BulkEmailJobArgs args)
        {
            // ── Load the operation ──
            var operation = await _operationRepository.GetAsync(args.OperationId);
            operation.Status = OperationStatus.InProgress;
            operation.StartedAt = Clock.Now;
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            // ── 1. Load active senders and compute remaining daily quotas ──
            var senders = await _senderRepository.GetAll()
                .Where(s => s.IsActive)
                .ToListAsync();

            if (!senders.Any())
            {
                operation.Status = OperationStatus.Failed;
                operation.CompletedAt = Clock.Now;
                await _operationRepository.UpdateAsync(operation);
                Logger.Error($"[BulkEmailSenderJob] Operation {args.OperationId}: No active email senders configured.");
                return;
            }

            var todayUtc = Clock.Now.Date;
            var activeSenders = new List<SenderQuota>();

            foreach (var sender in senders)
            {
                // Count sends across ALL operations today for this sender
                var sentToday = await _operationEmailRepository.GetAll()
                    .CountAsync(e => e.SenderId == sender.Id
                                  && e.Status == SendStatus.Success
                                  && e.SentAt.HasValue
                                  && e.SentAt.Value >= todayUtc);

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
                operation.Status = OperationStatus.Failed;
                operation.CompletedAt = Clock.Now;
                await _operationRepository.UpdateAsync(operation);
                Logger.Warn($"[BulkEmailSenderJob] Operation {args.OperationId}: No senders available. Aborting.");
                return;
            }

            Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId}: {activeSenders.Count} sender(s) ready. Subject: '{operation.Subject}'");

            // ── 2. Process emails for this operation ──
            var roundRobinIndex = 0;
            var totalSent = 0;
            var totalFailed = 0;
            var allSendersExhausted = false;

            try
            {
                var page = 0;
                while (true)
                {
                    var emails = await _operationEmailRepository.GetAll()
                        .Where(e => e.OperationId == args.OperationId && e.Status == SendStatus.Pending)
                        .OrderBy(e => e.Id)
                        .Skip(page * PageSize)
                        .Take(PageSize)
                        .ToListAsync();

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
                        if (email.RetryCount > 0)
                        {
                            var backoffMs = Math.Min(BaseBackoffMs * (1 << (email.RetryCount - 1)), MaxBackoffMs);
                            await Task.Delay(backoffMs);
                        }

                        // ── Send ──
                        var result = await _dispatcher.SendAsync(chosen.Client, chosen.Sender, email.Email, operation.Subject, operation.Body);

                        // ── Update email record ──
                        if (result.Success)
                        {
                            email.Status = SendStatus.Success;
                            email.SentAt = Clock.Now;
                            email.SenderId = chosen.Sender.Id;
                            totalSent++;
                            Logger.Debug($"[BulkEmailSenderJob] Sent to {email.Email} via {chosen.Sender.Email}");
                        }
                        else
                        {
                            email.RetryCount++;
                            email.ErrorMessage = result.ErrorMessage;
                            email.SenderId = chosen.Sender.Id;

                            if (email.RetryCount >= MaxRetries)
                            {
                                email.Status = SendStatus.Failed;
                                totalFailed++;
                            }
                            // else stays Pending for retry in next pass

                            Logger.Warn($"[BulkEmailSenderJob] Failed {email.Email} via {chosen.Sender.Email}: {result.ErrorMessage}");
                        }

                        await _operationEmailRepository.UpdateAsync(email);

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

            // ── 3. Update operation with final counts and status ──
            // Recompute counts from the database to handle retries correctly
            var sentCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Success);
            var failedCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Failed);
            var stillPending = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Pending);

            operation.SentCount = sentCount;
            operation.FailedCount = failedCount;

            if (stillPending > 0)
            {
                operation.Status = OperationStatus.PartiallySent;
            }
            else if (failedCount > 0 && sentCount > 0)
            {
                operation.Status = OperationStatus.PartiallySent;
            }
            else if (failedCount > 0 && sentCount == 0)
            {
                operation.Status = OperationStatus.Failed;
            }
            else
            {
                operation.Status = OperationStatus.Completed;
            }

            operation.CompletedAt = Clock.Now;
            await _operationRepository.UpdateAsync(operation);

            Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} finished. Sent: {totalSent}, Failed: {totalFailed}, StillPending: {stillPending}.");
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
