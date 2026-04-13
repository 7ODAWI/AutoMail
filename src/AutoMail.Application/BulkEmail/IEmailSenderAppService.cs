using Abp.Application.Services;
using AutoMail.BulkEmail.Dto;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AutoMail.BulkEmail
{
    public interface IEmailSenderAppService : IApplicationService
    {
        Task<List<EmailSenderDto>> GetAllAsync();
        Task<EmailSenderDto> GetAsync(int id);
        Task<EmailSenderDto> CreateAsync(CreateEmailSenderInput input);
        Task<EmailSenderDto> UpdateAsync(UpdateEmailSenderInput input);
        Task DeleteAsync(int id);
    }
}
