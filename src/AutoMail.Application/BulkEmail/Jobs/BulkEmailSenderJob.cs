using Abp.BackgroundJobs;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Timing;
using AutoMail.Project_Models;
using MailKit.Net.Smtp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Jobs
{
    public class BulkEmailSenderJob : AsyncBackgroundJob<BulkEmailJobArgs>, ITransientDependency
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> SenderLocks =
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        private readonly IRepository<EmailOperation, long> _operationRepository;
        private readonly IRepository<OperationEmail, long> _operationEmailRepository;
        private readonly IRepository<EmailSender, int> _senderRepository;
        private readonly IMailKitEmailDispatcher _dispatcher;
        private readonly IEmailOperationNotifier _notifier;
        private readonly IRepository<EmailTemplate, long> _templateRepository;
        private readonly BulkEmailSmtpReliabilityOptions _smtpOptions;
        private readonly SemaphoreSlim _globalSendGate;

        public BulkEmailSenderJob(
            IRepository<EmailOperation, long> operationRepository,
            IRepository<OperationEmail, long> operationEmailRepository,
            IRepository<EmailSender, int> senderRepository,
            IMailKitEmailDispatcher dispatcher,
            IEmailOperationNotifier notifier,
            IRepository<EmailTemplate, long> templateRepository,
            IConfiguration configuration)
        {
            _operationRepository = operationRepository;
            _operationEmailRepository = operationEmailRepository;
            _senderRepository = senderRepository;
            _dispatcher = dispatcher;
            _notifier = notifier;
            _templateRepository = templateRepository;
            _smtpOptions = BulkEmailSmtpReliabilityOptions.FromConfiguration(configuration);
            _globalSendGate = new SemaphoreSlim(_smtpOptions.MaxConcurrency, _smtpOptions.MaxConcurrency);
        }

        [UnitOfWork]
        public override async Task ExecuteAsync(BulkEmailJobArgs args)
        {
            var operation = await _operationRepository.GetAll()
                .FirstOrDefaultAsync(o => o.Id == args.OperationId);

            if (operation == null)
            {
                Logger.Warn($"[BulkEmailSenderJob] Operation {args.OperationId} was deleted before job start. Skipping.");
                return;
            }

            if (operation.Status == OperationStatus.InProgress)
            {
                Logger.Warn($"[BulkEmailSenderJob] Operation {args.OperationId} already in progress. Skipping duplicate job execution.");
                return;
            }

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

            var senders = await _senderRepository.GetAll()
                .Where(s => s.IsActive)
                .ToListAsync();

            var now = Clock.Now;
            var beforeCount = senders.Count;
            senders = senders.Where(s => !s.BlockedUntilUtc.HasValue || s.BlockedUntilUtc.Value <= now).ToList();
            var blockedCount = beforeCount - senders.Count;
            if (blockedCount > 0)
            {
                Logger.Info($"[BulkEmailSenderJob] Skipping {blockedCount} temporarily blocked sender(s).");
            }

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

                try
                {
                    var client = await ConnectFreshClientWithRetryAsync(sender, args.OperationId, "initial connect", CancellationToken.None);
                    activeSenders.Add(new SenderQuota
                    {
                        Sender = sender,
                        Remaining = remaining,
                        Client = client
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error($"[BulkEmailSenderJob] Failed to initialize sender '{sender.Email}': {ex.Message}", ex);
                }
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

            // Load templates from the shared global pool (OperationId == null).
            // These are the AI-generated templates produced by the standalone template generator.
            var templates = await _templateRepository.GetAll()
                .Where(t => t.OperationId == null)
                .ToListAsync();

            // Fallback: if no shared templates exist yet, try operation-specific ones
            if (templates.Count == 0)
            {
                templates = await _templateRepository.GetAll()
                    .Where(t => t.OperationId == args.OperationId)
                    .ToListAsync();
            }

            var rng = new Random();
            var roundRobinIndex = 0;
            var totalSent = operation.SentCount;
            var totalFailed = operation.FailedCount;
            var allSendersExhausted = false;
            var pausedOrCancelled = false;
            var emailsProcessedSinceCheck = 0;
            var currentSenderWindowCount = 0;
            SenderQuota currentSender = null;
            string stopReason = null;

            try
            {
                while (true)
                {
                    // Always fetch top pending page. Skip-based paging can skip rows as statuses mutate.
                    var emails = await _operationEmailRepository.GetAll()
                        .Where(e => e.OperationId == args.OperationId && e.Status == SendStatus.Pending)
                        .OrderBy(e => e.Id)
                        .Take(_smtpOptions.PageSize)
                        .ToListAsync();

                    if (!emails.Any())
                    {
                        break;
                    }

                    foreach (var email in emails)
                    {
                        if (emailsProcessedSinceCheck >= _smtpOptions.PauseCheckInterval)
                        {
                            emailsProcessedSinceCheck = 0;
                            var freshStatus = await _operationRepository.GetAll()
                                .AsNoTracking()
                                .Where(o => o.Id == args.OperationId)
                                .Select(o => (OperationStatus?)o.Status)
                                .FirstOrDefaultAsync();

                            if (!freshStatus.HasValue)
                            {
                                Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} was deleted during execution. Stopping job.");
                                return;
                            }

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

                        var stillPending = await _operationEmailRepository.GetAll()
                            .AsNoTracking()
                            .AnyAsync(e => e.Id == email.Id && e.Status == SendStatus.Pending);

                        if (!stillPending)
                        {
                            continue;
                        }

                        if (email.RetryCount > 0)
                        {
                            var retryBackoff = ComputeRetryDelayMs(email.RetryCount, rng);
                            await Task.Delay(retryBackoff);
                        }

                        if (currentSender == null
                            || currentSender.Remaining <= 0
                            || currentSenderWindowCount >= _smtpOptions.SenderSwitchEveryEmails)
                        {
                            currentSender = PickNextSender(activeSenders, ref roundRobinIndex);
                            currentSenderWindowCount = 0;
                        }

                        if (currentSender == null)
                        {
                            Logger.Warn("[BulkEmailSenderJob] All senders exhausted daily limits. Stopping.");
                            stopReason = "All sender daily limits were reached. Remaining emails will be sent when limits reset or after reactivation.";
                            allSendersExhausted = true;
                            break;
                        }

                        var (tSubject, tBody, tId) = PickTemplate(templates, operation, rng);
                        var sendResult = await SendWithRetryAndRecoveryAsync(
                            currentSender,
                            email.Email,
                            tSubject,
                            tBody,
                            args.OperationId,
                            CancellationToken.None,
                            rng);

                        if (sendResult.Success)
                        {
                            email.Status = SendStatus.Success;
                            email.SentAt = Clock.Now;
                            email.SenderId = currentSender.Sender.Id;
                            email.TemplateId = tId;
                            email.ErrorMessage = null;
                            totalSent++;

                            Logger.Debug($"[BulkEmailSenderJob] Sent to {email.Email} via {currentSender.Sender.Email}");
                        }
                        else
                        {
                            email.RetryCount++;
                            email.ErrorMessage = sendResult.ErrorMessage;
                            email.SenderId = currentSender.Sender.Id;

                            if (ShouldBlockSenderForDailyLimit(sendResult.ErrorMessage))
                            {
                                await BlockAndRemoveSenderAsync(activeSenders, currentSender, sendResult.ErrorMessage);
                                currentSender = null;
                                currentSenderWindowCount = 0;

                                if (!activeSenders.Any())
                                {
                                    Logger.Warn("[BulkEmailSenderJob] All senders exhausted after blocking. Stopping.");
                                    stopReason = "All sender daily limits were reached or blocked. Remaining emails will be sent when limits reset or after reactivation.";
                                    allSendersExhausted = true;
                                    break;
                                }

                                roundRobinIndex %= activeSenders.Count;
                            }

                            if (email.RetryCount >= _smtpOptions.RetryCount)
                            {
                                email.Status = SendStatus.Failed;
                                totalFailed++;
                            }

                            Logger.Warn($"[BulkEmailSenderJob] Failed {email.Email} via {email.SenderId}: {sendResult.ErrorMessage}. RetryCount={email.RetryCount}");
                        }

                        if (!await OperationExistsAsync(args.OperationId))
                        {
                            Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} was deleted before persisting send result. Stopping job.");
                            return;
                        }

                        await _operationEmailRepository.UpdateAsync(email);

                        operation.SentCount = totalSent;
                        operation.FailedCount = totalFailed;
                        await _operationRepository.UpdateAsync(operation);
                        await CurrentUnitOfWork.SaveChangesAsync();

                        emailsProcessedSinceCheck++;
                        currentSenderWindowCount++;

                        var pendingCount = operation.TotalEmails - totalSent - totalFailed;
                        await _notifier.NotifyEmailSentAsync(new EmailSentEvent
                        {
                            OperationId = operation.Id,
                            Email = email.Email,
                            Status = sendResult.Success ? "Sent" : "Failed",
                            SentAt = email.SentAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                            SenderEmail = currentSender?.Sender?.Email,
                            ErrorMessage = sendResult.Success ? null : sendResult.ErrorMessage,
                            SentCount = totalSent,
                            FailedCount = totalFailed,
                            PendingCount = Math.Max(0, pendingCount),
                            TotalEmails = operation.TotalEmails
                        });

                        if (currentSender != null)
                        {
                            currentSender.Remaining--;
                            if (currentSender.Remaining <= 0)
                            {
                                Logger.Info($"[BulkEmailSenderJob] Sender '{currentSender.Sender.Email}' reached daily limit. Disconnecting.");
                                await DisconnectSafelyAsync(currentSender.Client, currentSender.Sender.Email, "daily limit reached");
                                activeSenders.Remove(currentSender);
                                currentSender = null;
                                currentSenderWindowCount = 0;

                                if (!activeSenders.Any())
                                {
                                    Logger.Warn("[BulkEmailSenderJob] All senders exhausted. Stopping.");
                                    stopReason = "All sender daily limits were reached. Remaining emails will be sent when limits reset or after reactivation.";
                                    allSendersExhausted = true;
                                    break;
                                }

                                roundRobinIndex %= activeSenders.Count;
                            }
                        }

                        var delayMs = ComputeInterEmailDelayMs(currentSender?.Sender, rng);
                        if (delayMs > 0)
                        {
                            await Task.Delay(delayMs);
                        }
                    }

                    if (allSendersExhausted || pausedOrCancelled)
                    {
                        break;
                    }
                }
            }
            finally
            {
                foreach (var sender in activeSenders)
                {
                    await DisconnectSafelyAsync(sender.Client, sender.Sender.Email, "job final cleanup");
                }
            }

            var sentCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Success);
            var failedCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Failed);
            var pendingFinalCount = await _operationEmailRepository.GetAll()
                .CountAsync(e => e.OperationId == args.OperationId && e.Status == SendStatus.Pending);

            if (!await OperationExistsAsync(args.OperationId))
            {
                Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} was deleted before final status update. Skipping finalization.");
                return;
            }

            operation.SentCount = sentCount;
            operation.FailedCount = failedCount;

            if (stopReason != null)
            {
                operation.StopReason = stopReason;
            }

            var currentStatus = await _operationRepository.GetAll()
                .AsNoTracking()
                .Where(o => o.Id == args.OperationId)
                .Select(o => (OperationStatus?)o.Status)
                .FirstOrDefaultAsync();

            if (!currentStatus.HasValue)
            {
                Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} was deleted before final status read. Skipping finalization.");
                return;
            }

            if (currentStatus != OperationStatus.Paused && currentStatus != OperationStatus.Cancelled)
            {
                if (pendingFinalCount > 0)
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
            }

            await _operationRepository.UpdateAsync(operation);
            await CurrentUnitOfWork.SaveChangesAsync();

            await _notifier.NotifyStatusChangedAsync(BuildStatusEvent(operation));

            Logger.Info($"[BulkEmailSenderJob] Operation {args.OperationId} finished. Status: {operation.Status}. Sent: {sentCount}, Failed: {failedCount}, StillPending: {pendingFinalCount}.");
        }

        private async Task<bool> OperationExistsAsync(long operationId)
        {
            return await _operationRepository.GetAll()
                .AsNoTracking()
                .AnyAsync(o => o.Id == operationId);
        }

        private async Task<SendResult> SendWithRetryAndRecoveryAsync(
            SenderQuota senderQuota,
            string to,
            string subject,
            string body,
            long operationId,
            CancellationToken cancellationToken,
            Random rng)
        {
            string senderEmail = senderQuota.Sender.Email;
            var senderGate = SenderLocks.GetOrAdd(senderEmail, _ => new SemaphoreSlim(1, 1));

            for (var attempt = 1; attempt <= _smtpOptions.RetryCount; attempt++)
            {
                await _globalSendGate.WaitAsync(cancellationToken);
                await senderGate.WaitAsync(cancellationToken);

                try
                {
                    await EnsureHealthyConnectionAsync(senderQuota, operationId, cancellationToken);

                    var sendResult = await _dispatcher.SendAsync(
                        senderQuota.Client,
                        senderQuota.Sender,
                        to,
                        subject,
                        body,
                        _smtpOptions.SendTimeoutMs,
                        cancellationToken);

                    if (sendResult.Success)
                    {
                        senderQuota.SentSinceReconnect++;
                        return sendResult;
                    }

                    Logger.Warn($"[BulkEmailSenderJob] SMTP send failed. Op={operationId}, Sender={senderEmail}, Attempt={attempt}/{_smtpOptions.RetryCount}, Error={sendResult.ErrorMessage}");

                    if (sendResult.RequiresReconnect)
                    {
                        await RecoverSenderConnectionAsync(senderQuota, operationId, sendResult.ErrorMessage, cancellationToken);
                    }

                    if (!sendResult.IsTransient || attempt >= _smtpOptions.RetryCount)
                    {
                        return sendResult;
                    }
                }
                finally
                {
                    senderGate.Release();
                    _globalSendGate.Release();
                }

                var delayMs = ComputeRetryDelayMs(attempt, rng);
                Logger.Info($"[BulkEmailSenderJob] Retrying send after {delayMs}ms. Op={operationId}, Sender={senderEmail}, Attempt={attempt + 1}/{_smtpOptions.RetryCount}");
                await Task.Delay(delayMs, cancellationToken);
            }

            return new SendResult(false, "SMTP send failed after all retries.", true, false);
        }

        private async Task EnsureHealthyConnectionAsync(SenderQuota senderQuota, long operationId, CancellationToken cancellationToken)
        {
            if (senderQuota.Client == null || !senderQuota.Client.IsConnected)
            {
                await RecoverSenderConnectionAsync(senderQuota, operationId, "client not connected", cancellationToken);
                return;
            }

            if (!senderQuota.Client.IsAuthenticated)
            {
                await RecoverSenderConnectionAsync(senderQuota, operationId, "client not authenticated", cancellationToken);
                return;
            }

            if (senderQuota.SentSinceReconnect >= _smtpOptions.ReconnectEveryEmails)
            {
                await RecoverSenderConnectionAsync(senderQuota, operationId, $"periodic reconnect after {senderQuota.SentSinceReconnect} emails", cancellationToken);
            }
        }

        private async Task RecoverSenderConnectionAsync(SenderQuota senderQuota, long operationId, string reason, CancellationToken cancellationToken)
        {
            Logger.Info($"[BulkEmailSenderJob] Reconnecting sender '{senderQuota.Sender.Email}'. Op={operationId}, Reason={reason}");

            await DisconnectSafelyAsync(senderQuota.Client, senderQuota.Sender.Email, reason);
            senderQuota.Client = await ConnectFreshClientWithRetryAsync(senderQuota.Sender, operationId, reason, cancellationToken);
            senderQuota.SentSinceReconnect = 0;
        }

        private async Task<SmtpClient> ConnectFreshClientWithRetryAsync(
            EmailSender sender,
            long operationId,
            string reason,
            CancellationToken cancellationToken)
        {
            Exception lastError = null;
            var rng = new Random();

            for (var attempt = 1; attempt <= _smtpOptions.RetryCount; attempt++)
            {
                try
                {
                    Logger.Info($"[BulkEmailSenderJob] Connecting sender '{sender.Email}'. Op={operationId}, Attempt={attempt}/{_smtpOptions.RetryCount}, Reason={reason}");
                    return await _dispatcher.ConnectAsync(
                        sender,
                        _smtpOptions.SocketTimeoutMs,
                        _smtpOptions.ConnectTimeoutMs,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Logger.Warn($"[BulkEmailSenderJob] Connect failed for '{sender.Email}'. Op={operationId}, Attempt={attempt}/{_smtpOptions.RetryCount}, Error={ex.Message}");

                    if (attempt >= _smtpOptions.RetryCount)
                    {
                        break;
                    }

                    var delayMs = ComputeRetryDelayMs(attempt, rng);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            throw new InvalidOperationException(
                $"Failed to connect sender '{sender.Email}' after {_smtpOptions.RetryCount} attempts. LastError={lastError?.Message}",
                lastError);
        }

        private async Task BlockAndRemoveSenderAsync(List<SenderQuota> activeSenders, SenderQuota senderQuota, string reason)
        {
            try
            {
                senderQuota.Sender.BlockedUntilUtc = Clock.Now.Date.AddDays(1);
                senderQuota.Sender.BlockReason = "Daily sending limit exceeded";
                await _senderRepository.UpdateAsync(senderQuota.Sender);
                await CurrentUnitOfWork.SaveChangesAsync();

                Logger.Warn($"[BulkEmailSenderJob] Blocking sender '{senderQuota.Sender.Email}' until {senderQuota.Sender.BlockedUntilUtc}. Reason={reason}");
                await DisconnectSafelyAsync(senderQuota.Client, senderQuota.Sender.Email, "sender temporarily blocked");
                activeSenders.Remove(senderQuota);
            }
            catch (Exception ex)
            {
                Logger.Error($"[BulkEmailSenderJob] Failed to block sender '{senderQuota.Sender.Email}': {ex.Message}", ex);
            }
        }

        private async Task DisconnectSafelyAsync(SmtpClient client, string senderEmail, string reason)
        {
            if (client == null)
            {
                return;
            }

            try
            {
                if (client.IsConnected)
                {
                    Logger.Info($"[BulkEmailSenderJob] Disconnecting sender '{senderEmail}'. Reason={reason}");
                    await client.DisconnectAsync(true);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"[BulkEmailSenderJob] Safe disconnect failed for '{senderEmail}'. Reason={reason}. Error={ex.Message}");
            }
            finally
            {
                client.Dispose();
            }
        }

        private int ComputeRetryDelayMs(int retryAttempt, Random rng)
        {
            var exponential = _smtpOptions.RetryBaseDelayMs * (1 << Math.Max(0, retryAttempt - 1));
            var bounded = Math.Min(exponential, _smtpOptions.RetryMaxDelayMs);
            var jitter = _smtpOptions.RetryJitterMs > 0 ? rng.Next(0, _smtpOptions.RetryJitterMs + 1) : 0;
            return bounded + jitter;
        }

        private int ComputeInterEmailDelayMs(EmailSender sender, Random rng)
        {
            var randomizedMs = rng.Next(_smtpOptions.DelayMinSeconds, _smtpOptions.DelayMaxSeconds + 1) * 1000;
            var senderConfiguredDelay = sender?.DelayBetweenEmailsMs ?? 0;
            return Math.Max(senderConfiguredDelay, randomizedMs);
        }

        private static bool ShouldBlockSenderForDailyLimit(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return false;
            }

            return errorMessage.IndexOf("Daily user sending limit", StringComparison.OrdinalIgnoreCase) >= 0
                || errorMessage.IndexOf("daily sending limit", StringComparison.OrdinalIgnoreCase) >= 0
                || errorMessage.IndexOf("5.4.5", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static SenderQuota PickNextSender(List<SenderQuota> activeSenders, ref int roundRobinIndex)
        {
            for (var i = 0; i < activeSenders.Count; i++)
            {
                var idx = (roundRobinIndex + i) % activeSenders.Count;
                if (activeSenders[idx].Remaining > 0)
                {
                    roundRobinIndex = (idx + 1) % activeSenders.Count;
                    return activeSenders[idx];
                }
            }

            return null;
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

        private static (string subject, string body, long? templateId) PickTemplate(
            List<EmailTemplate> templates,
            EmailOperation operation,
            Random rng)
        {
            if (templates == null || templates.Count == 0)
            {
                return (operation.Subject, operation.Body, null);
            }

            if (templates.Count == 1)
            {
                return (templates[0].Subject, templates[0].Body, templates[0].Id);
            }

            var totalWeight = templates.Sum(t => t.Weight);
            var roll = rng.Next(totalWeight);
            var cumulative = 0;

            foreach (var template in templates)
            {
                cumulative += template.Weight;
                if (roll < cumulative)
                {
                    return (template.Subject, template.Body, template.Id);
                }
            }

            var last = templates[templates.Count - 1];
            return (last.Subject, last.Body, last.Id);
        }

        private class SenderQuota
        {
            public EmailSender Sender { get; set; }
            public int Remaining { get; set; }
            public SmtpClient Client { get; set; }
            public int SentSinceReconnect { get; set; }
        }
    }
}
