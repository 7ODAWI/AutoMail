using Abp.Application.Services;
using AutoMail.Authorization.Accounts.Dto;
using System.Threading.Tasks;

namespace AutoMail.Authorization.Accounts;

public interface IAccountAppService : IApplicationService
{
    Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input);

    Task<RegisterOutput> Register(RegisterInput input);
}
