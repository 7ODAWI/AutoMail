using Abp.Events.Bus;
using Abp.Modules;
using Abp.Reflection.Extensions;
using AutoMail.Configuration;
using AutoMail.EntityFrameworkCore;
using AutoMail.Migrator.DependencyInjection;
using Castle.MicroKernel.Registration;
using Microsoft.Extensions.Configuration;

namespace AutoMail.Migrator;

[DependsOn(typeof(AutoMailEntityFrameworkModule))]
public class AutoMailMigratorModule : AbpModule
{
    private readonly IConfigurationRoot _appConfiguration;

    public AutoMailMigratorModule(AutoMailEntityFrameworkModule abpProjectNameEntityFrameworkModule)
    {
        abpProjectNameEntityFrameworkModule.SkipDbSeed = true;

        _appConfiguration = AppConfigurations.Get(
            typeof(AutoMailMigratorModule).GetAssembly().GetDirectoryPathOrNull()
        );
    }

    public override void PreInitialize()
    {
        Configuration.DefaultNameOrConnectionString = _appConfiguration.GetConnectionString(
            AutoMailConsts.ConnectionStringName
        );

        Configuration.BackgroundJobs.IsJobExecutionEnabled = false;
        Configuration.ReplaceService(
            typeof(IEventBus),
            () => IocManager.IocContainer.Register(
                Component.For<IEventBus>().Instance(NullEventBus.Instance)
            )
        );
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(AutoMailMigratorModule).GetAssembly());
        ServiceCollectionRegistrar.Register(IocManager);
    }
}
