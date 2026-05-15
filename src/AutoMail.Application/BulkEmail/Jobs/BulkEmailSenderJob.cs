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
        // Check pause/stop signal every N emails to avoid hammering DB
        private const int PauseCheckInterval = 10;

        private readonly IRepository<EmailOperation, long> _operationRepository;
        private readonly IRepository<OperationEmail, long> _operationEmailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IMailKitEmailDispatcher _dispatcher;
        private readonly IEmailOperationNotifier _notifier;

        public BulkEmailSenderJob(
            IRepository<EmailOperation, long> operationRepository,
            IRepository<OperationEmail, long> operationEmailRepository,
            IRepository<EmailSender, int> senderRepository,
            IMailKitEmailDispatcher dispatcher,
            IEmailOperationNotifier notifier)
        {
            _operationRepository = operationRepository;
            _operationEmailRepository = operationEmailRepository;
            _senderRepository = senderRepository;
            _dispatcher = dispatcher;
            _notifier = notifier;
        }

        [UnitOfWork]
        public override async Task ExecuteAsync(BulkEmailJobArgs args)
        {
            // ── Load the operation ──
            var operation = await _operationRepository.GetAsync(args.OperationId);

            // Guard: skip if already paused or cancelled (race condition on reactivation)
            if (operation.Status == OperationStatus.Paused || operation.Status == OperationStatus.Cancelled)
            {
                Logger.Warn($"[BulkEmailSenderJob] Operation {args.OperationId} is {operation.Status}. Skipping.");
                return;
            }

            operation.Status = OperationStatus.InProgress;
            operation.StartedAt = Clock.Now;
            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            await _notifier.NotifyStatusChangedAsync(new OperationStatusChangedEvent
            {
                OperationId = operation.Id,
                Status = "InProgress",
                SentCount = operation.SentCount,
                FailedCount = operation.FailedCount,
                PendingCount = operation.TotalEmails - operation.SentCount - operation.FailedCount,
                TotalEmails = operation.TotalEmails
            });

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
                await _notifier.NotifyStatusChangedAsync(BuildStatusEvent(operation));
                return;
            }

            var todayUtc = Clock.Now.Date;
            var activeSenders = new List<SenderQuota>();

            foreach (var sender in senders)
            {
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
                await _notifier.NotifyStatusChangedAsync(BuildStatusEvent(operation));
                return;
            }

            Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId}: {activeSenders.Count} sender(s) ready. Subject: '{operation.Subject}'");

            // ── 2. Process emails for this operation ──
            var roundRobinIndex = 0;
            var totalSent = operation.SentCount;
            var totalFailed = operation.FailedCount;
            var allSendersExhausted = false;
            var pausedOrCancelled = false;
            var emailsProcessedSinceCheck = 0;
            string stopReason = null;

            try
            {
                var page = 0;
                while (true)
                {
                    // Reload pending emails for this page
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
                        // ── Check pause/stop every PauseCheckInterval emails ──
                        if (emailsProcessedSinceCheck >= PauseCheckInterval)
                        {
                            emailsProcessedSinceCheck = 0;
                            // AsNoTracking + scalar select: bypasses EF identity map so we
                            // always read the value the UI wrote, not the cached tracked entity.
                            var freshStatus = await _operationRepository.GetAll()
                                .AsNoTracking()
                                .Where(o => o.Id == args.OperationId)
                                .Select(o => o.Status)
                                .FirstAsync();

                            if (freshStatus == OperationStatus.Paused)
                            {
                                Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} paused by user.");
                                stopReason = "Paused by user.";
                                pausedOrCancelled = true;
                                break;
                            }
                            if (freshStatus == OperationStatus.Cancelled)
                            {
                                Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} cancelled by user.");
                                stopReason = "Cancelled by user.";
                                pausedOrCancelled = true;
                                break;
                            }
                        }

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
                            stopReason = "All sender daily limits were reached. Remaining emails will be sent when limits reset or after reactivation.";
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

                            Logger.Warn($"[BulkEmailSenderJob] Failed {email.Email} via {chosen.Sender.Email}: {result.ErrorMessage}");
                        }

                        await _operationEmailRepository.UpdateAsync(email);

                        // ── Update live operation counts ──
                        operation.SentCount = totalSent;
                        operation.FailedCount = totalFailed;
                        await _operationRepository.UpdateAsync(operation);
                        await CurrentUnitOfWork.SaveChangesAsync();

                        emailsProcessedSinceCheck++;

                        // ── Push real-time notification ──
                        var pendingCount = operation.TotalEmails - totalSent - totalFailed;
                        await _notifier.NotifyEmailSentAsync(new EmailSentEvent
                        {
                            OperationId = operation.Id,
                            Email = email.Email,
                            Status = result.Success ? "Sent" : "Failed",
                            SentAt = email.SentAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                            SenderEmail = chosen.Sender.Email,
                            ErrorMessage = result.Success ? null : result.ErrorMessage,
                            SentCount = totalSent,
                            FailedCount = totalFailed,
                            PendingCount = Math.Max(0, pendingCount),
                            TotalEmails = operation.TotalEmails
                        });

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
                                stopReason = "All sender daily limits were reached. Remaining emails will be sent when limits reset or after reactivation.";
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

                    if (allSendersExhausted || pausedOrCancelled)
                        break;

                    page++;
                }
            }
            finally
            {
                foreach (var sq in activeSenders)
                {
                    await DisconnectSafely(sq.Client);
                }
            }

            // ── 3. Update operation final status ──
            // Reload counts from DB to be precise
            var sentCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Success);
            var failedCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Failed);
            var stillPending = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Pending);

            operation.SentCount = sentCount;
            operation.FailedCount = failedCount;

            // Record stop reason if any
            if (stopReason != null)
                operation.StopReason = stopReason;

            // If already Paused/Cancelled by user, preserve that status.
            // Must use AsNoTracking so EF doesn't return the stale tracked entity.
            var currentStatus = await _operationRepository.GetAll()
                .AsNoTracking()
                .Where(o => o.Id == args.OperationId)
                .Select(o => o.Status)
                .FirstAsync();
            if (currentStatus != OperationStatus.Paused && currentStatus != OperationStatus.Cancelled)
            {
                if (stillPending > 0)
                    operation.Status = OperationStatus.PartiallySent;
                else if (failedCount > 0 && sentCount > 0)
                    operation.Status = OperationStatus.PartiallySent;
                else if (failedCount > 0 && sentCount == 0)
                    operation.Status = OperationStatus.Failed;
                else
                    operation.Status = OperationStatus.Completed;

                operation.CompletedAt = Clock.Now;
            }

            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            await _notifier.NotifyStatusChangedAsync(BuildStatusEvent(operation));

            Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} finished. Status: {operation.Status}. Sent: {sentCount}, Failed: {failedCount}, StillPending: {stillPending}.");
        }

        private static OperationStatusChangedEvent BuildStatusEvent(EmailOperation op) =>
            new OperationStatusChangedEvent
            {
                OperationId = op.Id,
                Status = op.Status.ToString(),
                SentCount = op.SentCount,
                FailedCount = op.FailedCount,
                PendingCount = Math.Max(0, op.TotalEmails - op.SentCount - op.FailedCount),
                TotalEmails = op.TotalEmails
            };

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
