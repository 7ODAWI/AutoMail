using Abp.Application.Services;
using AutoMail.Sessions.Dto;
using System.Threading.Tasks;

namespace AutoMail.Sessions;

public interface ISessionAppService : IApplicationService
{
    Task<GetCurrentLoginInformationsOutput> GetCurrentLoginInformations();
}
