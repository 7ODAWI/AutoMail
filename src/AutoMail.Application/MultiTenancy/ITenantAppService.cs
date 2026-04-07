using Abp.Application.Services;
using AutoMail.MultiTenancy.Dto;

namespace AutoMail.MultiTenancy;

public interface ITenantAppService : IAsyncCrudAppService<TenantDto, int, PagedTenantResultRequestDto, CreateTenantDto, TenantDto>
{
}

