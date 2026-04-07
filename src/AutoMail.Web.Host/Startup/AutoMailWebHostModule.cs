using Abp.Modules;
using Abp.Reflection.Extensions;
using AutoMail.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AutoMail.Web.Host.Startup
{
    [DependsOn(
       typeof(AutoMailWebCoreModule))]
    public class AutoMailWebHostModule : AbpModule
    {
        private readonly IWebHostEnvironment _env;
        private readonly IConfigurationRoot _appConfiguration;

        public AutoMailWebHostModule(IWebHostEnvironment env)
        {
            _env = env;
            _appConfiguration = env.GetAppConfiguration();
        }

        public override void Initialize()
        {
            IocManager.RegisterAssemblyByConvention(typeof(AutoMailWebHostModule).GetAssembly());
        }
    }
}
