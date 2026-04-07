using Abp.AspNetCore.Mvc.ViewComponents;

namespace AutoMail.Web.Views;

public abstract class AutoMailViewComponent : AbpViewComponent
{
    protected AutoMailViewComponent()
    {
        LocalizationSourceName = AutoMailConsts.LocalizationSourceName;
    }
}
