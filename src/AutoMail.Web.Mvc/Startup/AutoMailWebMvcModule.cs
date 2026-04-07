using Abp.Modules;
using Abp.Reflection.Extensions;
using AutoMail.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AutoMail.Web.Startup;

[DependsOn(typeof(AutoMailWebCoreModule))]
public class AutoMailWebMvcModule : AbpModule
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfigurationRoot _appConfiguration;

    public AutoMailWebMvcModule(IWebHostEnvironment env)
    {
        _env = env;
        _appConfiguration = env.GetAppConfiguration();
    }

    public override void PreInitialize()
    {
        Configuration.Navigation.Providers.Add<AutoMailNavigationProvider>();
    }

    public override void Initialize()
    {
        IocManager.RegisterAssemblyByConvention(typeof(AutoMailWebMvcModule).GetAssembly());
    }
}
