using Abp.AutoMapper;
using Abp.Modules;
using Abp.Reflection.Extensions;
using AutoMail.Authorization;

namespace AutoMail;

[DependsOn(
    typeof(AutoMailCoreModule),
    typeof(AbpAutoMapperModule))]
public class AutoMailApplicationModule : AbpModule
{
    public override void PreInitialize()
    {
        Configuration.Authorization.Providers.Add<AutoMailAuthorizationProvider>();
    }

    public override void Initialize()
    {
        var thisAssembly = typeof(AutoMailApplicationModule).GetAssembly();

        IocManager.RegisterAssemblyByConvention(thisAssembly);

        Configuration.Modules.AbpAutoMapper().Configurators.Add(
            // Scan the assembly for classes which inherit from AutoMapper.Profile
            cfg => cfg.AddMaps(thisAssembly)
        );
    }
}
