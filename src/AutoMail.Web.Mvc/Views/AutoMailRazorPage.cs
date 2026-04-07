using Abp.AspNetCore.Mvc.Views;
using Abp.Runtime.Session;
using Microsoft.AspNetCore.Mvc.Razor.Internal;

namespace AutoMail.Web.Views;

public abstract class AutoMailRazorPage<TModel> : AbpRazorPage<TModel>
{
    [RazorInject]
    public IAbpSession AbpSession { get; set; }

    protected AutoMailRazorPage()
    {
        LocalizationSourceName = AutoMailConsts.LocalizationSourceName;
    }
}
