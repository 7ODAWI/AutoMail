using Abp.Dependency;
using AutoMail.Project_Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail.Jobs
{
    public record SendResult(bool Success, string ErrorMessage);

    public interface IMailKitEmailDispatcher
    {
        /// <summary>
        /// Creates a connected and authenticated SmtpClient for the given sender.
        /// Caller is responsible for disconnecting/disposing.
        /// </summary>
        Task<SmtpClient> ConnectAsync(EmailSender sender);

        /// <summary>
        /// Sends a single email using an already-connected SmtpClient.
        /// </summary>
        Task<SendResult> SendAsync(SmtpClient client, EmailSender sender, string to, string subject, string body);
    }

    public class MailKitEmailDispatcher : IMailKitEmailDispatcher, ITransientDependency
    {
        public async Task<SmtpClient> ConnectAsync(EmailSender sender)
        {
            var client = new SmtpClient();

            var secureOption = sender.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

            await client.ConnectAsync(sender.SmtpHost, sender.SmtpPort, secureOption);
            await client.AuthenticateAsync(sender.Email, sender.Password);

            return client;
        }

        public async Task<SendResult> SendAsync(SmtpClient client, EmailSender sender, string to, string subject, string body)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(sender.DisplayName ?? sender.Email, sender.Email));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                message.Body = new TextPart("html") { Text = body };

                await client.SendAsync(message);

                return new SendResult(true, null);
            }
            catch (Exception ex)
            {
                return new SendResult(false, ex.Message);
            }
        }
    }
}
