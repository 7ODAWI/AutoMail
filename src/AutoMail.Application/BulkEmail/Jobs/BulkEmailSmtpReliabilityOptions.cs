using Microsoft.Extensions.Configuration;
using System;

namespace AutoMail.BulkEmail.Jobs
{
    public class BulkEmailSmtpReliabilityOptions
    {
        public int RetryCount { get; set; } = 3;
        public int RetryBaseDelayMs { get; set; } = 2000;
        public int RetryMaxDelayMs { get; set; } = 30000;
        public int RetryJitterMs { get; set; } = 1500;

        public int ReconnectEveryEmails { get; set; } = 25;
        public int SenderSwitchEveryEmails { get; set; } = 1;
        public int PauseCheckInterval { get; set; } = 10;
        public int PageSize { get; set; } = 200;

        public int DelayMinSeconds { get; set; } = 4;
        public int DelayMaxSeconds { get; set; } = 12;

        public int MaxConcurrency { get; set; } = 2;

        public int ConnectTimeoutMs { get; set; } = 30000;
        public int SendTimeoutMs { get; set; } = 45000;
        public int SocketTimeoutMs { get; set; } = 120000;

        public static BulkEmailSmtpReliabilityOptions FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection("BulkEmail:SmtpReliability");
            var options = section.Get<BulkEmailSmtpReliabilityOptions>() ?? new BulkEmailSmtpReliabilityOptions();
            options.Normalize();
            return options;
        }

        public void Normalize()
        {
            RetryCount = Math.Max(3, RetryCount);
            RetryBaseDelayMs = Math.Max(250, RetryBaseDelayMs);
            RetryMaxDelayMs = Math.Max(RetryBaseDelayMs, RetryMaxDelayMs);
            RetryJitterMs = Math.Max(0, RetryJitterMs);

            ReconnectEveryEmails = Math.Max(5, ReconnectEveryEmails);
            SenderSwitchEveryEmails = Math.Max(1, SenderSwitchEveryEmails);
            PauseCheckInterval = Math.Max(1, PauseCheckInterval);
            PageSize = Math.Clamp(PageSize, 50, 1000);

            DelayMinSeconds = Math.Max(1, DelayMinSeconds);
            DelayMaxSeconds = Math.Max(DelayMinSeconds, DelayMaxSeconds);

            MaxConcurrency = Math.Clamp(MaxConcurrency, 1, 10);

            ConnectTimeoutMs = Math.Max(5000, ConnectTimeoutMs);
            SendTimeoutMs = Math.Max(5000, SendTimeoutMs);
            SocketTimeoutMs = Math.Max(5000, SocketTimeoutMs);
        }
    }
}