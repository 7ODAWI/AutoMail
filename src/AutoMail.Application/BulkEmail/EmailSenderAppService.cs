using Abp.Domain.Repositories;
using Abp.UI;
using AutoMail.BulkEmail.Dto;
using AutoMail.Project_Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public class EmailSenderAppService : AutoMailAppServiceBase, IEmailSenderAppService
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IRepository<EmailSender, int> _senderRepository;

        public EmailSenderAppService(IRepository<EmailSender, int> senderRepository)
        {
            _senderRepository = senderRepository;
        }

        public async Task<List<EmailSenderDto>> GetAllAsync()
        {
            var senders = await _senderRepository.GetAll()
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.Email)
                .ToListAsync();

            return senders.Select(MapToDto).ToList();
        }

        public async Task<EmailSenderDto> GetAsync(int id)
        {
            var sender = await _senderRepository.GetAsync(id);
            return MapToDto(sender);
        }

        public async Task<EmailSenderDto> CreateAsync(CreateEmailSenderInput input)
        {
            if (!EmailRegex.IsMatch(input.Email))
                throw new UserFriendlyException("Please enter a valid email address.");

            using (CurrentUnitOfWork.DisableFilter(Abp.Domain.Uow.AbpDataFilters.SoftDelete))
            {
                var existing = await _senderRepository.GetAll()
                    .FirstOrDefaultAsync(s => s.Email == input.Email.Trim().ToLowerInvariant());

                if (existing != null)
                {
                    if (!existing.IsDeleted)
                        throw new UserFriendlyException($"A sender with email '{input.Email}' already exists.");

                    // Un-delete the previously soft-deleted sender and update it
                    existing.IsDeleted = false;
                    existing.DeletionTime = null;
                    existing.DeleterUserId = null;
                    existing.Password = input.Password;
                    existing.DisplayName = input.DisplayName?.Trim();
                    existing.SmtpHost = input.SmtpHost?.Trim() ?? "smtp.gmail.com";
                    existing.SmtpPort = input.SmtpPort;
                    existing.EnableSsl = input.EnableSsl;
                    existing.IsActive = input.IsActive;
                    existing.DailyLimit = input.DailyLimit;
                    existing.DelayBetweenEmailsMs = input.DelayBetweenEmailsMs;

                    await CurrentUnitOfWork.SaveChangesAsync();
                    return MapToDto(existing);
                }
            }

            var sender = new EmailSender
            {
                Email = input.Email.Trim().ToLowerInvariant(),
                Password = input.Password,
                DisplayName = input.DisplayName?.Trim(),
                SmtpHost = input.SmtpHost?.Trim() ?? "smtp.gmail.com",
                SmtpPort = input.SmtpPort,
                EnableSsl = input.EnableSsl,
                IsActive = input.IsActive,
                DailyLimit = input.DailyLimit,
                DelayBetweenEmailsMs = input.DelayBetweenEmailsMs
            };

            sender = await _senderRepository.InsertAsync(sender);
            await CurrentUnitOfWork.SaveChangesAsync();

            return MapToDto(sender);
        }

        public async Task<EmailSenderDto> UpdateAsync(UpdateEmailSenderInput input)
        {
            if (!EmailRegex.IsMatch(input.Email))
                throw new UserFriendlyException("Please enter a valid email address.");

            var sender = await _senderRepository.GetAsync(input.Id);

            var normalizedEmail = input.Email.Trim().ToLowerInvariant();
            if (sender.Email != normalizedEmail)
            {
                var duplicate = await _senderRepository.GetAll()
                    .AnyAsync(s => s.Email == normalizedEmail && s.Id != input.Id);

                if (duplicate)
                    throw new UserFriendlyException($"A sender with email '{input.Email}' already exists.");
            }

            sender.Email = normalizedEmail;
            sender.DisplayName = input.DisplayName?.Trim();
            sender.SmtpHost = input.SmtpHost?.Trim() ?? "smtp.gmail.com";
            sender.SmtpPort = input.SmtpPort;
            sender.EnableSsl = input.EnableSsl;
            sender.IsActive = input.IsActive;
            sender.DailyLimit = input.DailyLimit;
            sender.DelayBetweenEmailsMs = input.DelayBetweenEmailsMs;

            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                sender.Password = input.Password;
            }

            await _senderRepository.UpdateAsync(sender);

            return MapToDto(sender);
        }
        public async Task<List<string>> GetDistinctEmailsPAsync()
        {
           return await _senderRepository.GetAll()
                .Where(s => !s.IsDeleted)
                .Select(s => s.Email)
                .Distinct()
                .ToListAsync();
        }

        public async Task DeleteAsync(int id)
        {
            await _senderRepository.DeleteAsync(id);
        }

        private static EmailSenderDto MapToDto(EmailSender sender)
        {
            return new EmailSenderDto
            {
                Id = sender.Id,
                Email = sender.Email,
                DisplayName = sender.DisplayName,
                SmtpHost = sender.SmtpHost,
                SmtpPort = sender.SmtpPort,
                EnableSsl = sender.EnableSsl,
                IsActive = sender.IsActive,
                DailyLimit = sender.DailyLimit,
                DelayBetweenEmailsMs = sender.DelayBetweenEmailsMs,
                HasPassword = !string.IsNullOrWhiteSpace(sender.Password)
            };
        }
    }
}
