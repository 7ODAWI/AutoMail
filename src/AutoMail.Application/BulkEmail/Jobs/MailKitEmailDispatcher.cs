using Abp.Dependency;
using AutoMail.Project_Models;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Jobs
{
    public record SendResult(bool Success, string ErrorMessage, bool IsTransient = false, bool RequiresReconnect = false);

    public interface IMailKitEmailDispatcher
    {
        /// <summary>
        /// Creates a connected and authenticated SmtpClient for the given sender.
        /// Caller is responsible for disconnecting/disposing.
        /// </summary>
        Task<SmtpClient> ConnectAsync(
            EmailSender sender,
            int socketTimeoutMs,
            int connectTimeoutMs,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a single email using an already-connected SmtpClient.
        /// </summary>
        Task<SendResult> SendAsync(
            SmtpClient client,
            EmailSender sender,
            string to,
            string subject,
            string body,
            int sendTimeoutMs,
            CancellationToken cancellationToken = default);
    }

    public class MailKitEmailDispatcher : IMailKitEmailDispatcher, ITransientDependency
    {
        public async Task<SmtpClient> ConnectAsync(
            EmailSender sender,
            int socketTimeoutMs,
            int connectTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            var client = new SmtpClient();
            client.Timeout = socketTimeoutMs;
            client.AuthenticationMechanisms.Remove("XOAUTH2");

            var secureOption = sender.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(connectTimeoutMs);

            await client.ConnectAsync(sender.SmtpHost, sender.SmtpPort, secureOption, timeoutCts.Token);
            await client.AuthenticateAsync(sender.Email, sender.Password, timeoutCts.Token);

            return client;
        }

        public async Task<SendResult> SendAsync(
            SmtpClient client,
            EmailSender sender,
            string to,
            string subject,
            string body,
            int sendTimeoutMs,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (client == null || !client.IsConnected)
                {
                    return new SendResult(false, "The SmtpClient is not connected", true, true);
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(sender.DisplayName ?? sender.Email, sender.Email));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                message.Body = new TextPart("html") { Text = body };

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(sendTimeoutMs);

                await client.SendAsync(message, timeoutCts.Token);

                return new SendResult(true, null);
            }
            catch (Exception ex)
            {
                var flattenedMessage = FlattenMessage(ex);
                var isExpired = IsConnectionExpired(flattenedMessage);
                var isDisconnected = IsDisconnected(flattenedMessage);
                var hasSocketError = ex is SocketException || ex.InnerException is SocketException;
                var isTimeout = ex is TimeoutException || ex is OperationCanceledException;
                var isMailKitTransient = ex is ServiceNotConnectedException
                    || ex is ServiceNotAuthenticatedException
                    || ex is SmtpCommandException
                    || ex is SmtpProtocolException;

                var transient = isExpired || isDisconnected || hasSocketError || isTimeout || isMailKitTransient;
                var requiresReconnect = isExpired
                    || isDisconnected
                    || hasSocketError
                    || ex is ServiceNotConnectedException
                    || ex is ServiceNotAuthenticatedException
                    || ex is SmtpProtocolException;

                return new SendResult(false, flattenedMessage, transient, requiresReconnect);
            }
        }

        private static bool IsConnectionExpired(string message) =>
            message?.IndexOf("4.7.0 Connection expired", StringComparison.OrdinalIgnoreCase) >= 0
            || message?.IndexOf("connection expired", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsDisconnected(string message) =>
            message?.IndexOf("The SmtpClient is not connected", StringComparison.OrdinalIgnoreCase) >= 0
            || message?.IndexOf("not connected", StringComparison.OrdinalIgnoreCase) >= 0
            || message?.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0;

        private static string FlattenMessage(Exception ex)
        {
            if (ex == null)
            {
                return "Unknown SMTP error";
            }

            if (ex.InnerException == null)
            {
                return ex.Message;
            }

            return $"{ex.Message} | Inner: {FlattenMessage(ex.InnerException)}";
        }
    }
}
