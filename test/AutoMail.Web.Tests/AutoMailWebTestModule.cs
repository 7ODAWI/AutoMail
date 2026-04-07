using Abp.AspNetCore;
using Abp.AspNetCore.TestBase;
using Abp.Modules;
using Abp.Reflection.Extensions;
using AutoMail.EntityFrameworkCore;
using AutoMail.Web.Startup;
using Microsoft.AspNetCore.Mvc.ApplicationParts;

namespace AutoMail.Web.Tests;

[DependsOn(
    typeof(AutoMailWebMvcModule),
    typeof(AbpAspNetCoreTestBaseModule)
)]
public class AutoMailWebTestModule : AbpModule
{
    public AutoMailWebTestModule(AutoMailEntityFrameworkModule abpProjectNameEntityFrameworkModule)
    {
        abpProjectNameEntityFrameworkModule.SkipDbContextRegistration = true;
    }

    public override void PreInitialize()
    {
        Configuration.UnitOfWork.IsTransactional = false; //EF Core InMemory DB does not support transactions.
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(AutoMailWebTestModule).GetAssembly());
    }

    public override void PostInitialize()
    {
        IocManager.Resolve<ApplicationPartManager>()
            .AddApplicationPartsIfNotAddedBefore(typeof(AutoMailWebMvcModule).Assembly);
    }
}