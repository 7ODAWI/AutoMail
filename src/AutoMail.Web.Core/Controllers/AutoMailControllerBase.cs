using Abp.AspNetCore.Mvc.Controllers;
using Abp.IdentityFramework;
using Microsoft.AspNetCore.Identity;

namespace AutoMail.Controllers
{
    public abstract class AutoMailControllerBase : AbpController
    {
        protected AutoMailControllerBase()
        {
            LocalizationSourceName = AutoMailConsts.LocalizationSourceName;
        }

        protected void CheckErrors(IdentityResult identityResult)
        {
            identityResult.CheckErrors(LocalizationManager);
        }
    }
}
