using Abp.Authorization;
using Abp.Runtime.Session;
using AutoMail.Configuration.Dto;
using System.Threading.Tasks;

namespace AutoMail.Configuration;

[AbpAuthorize]
public class ConfigurationAppService : AutoMailAppServiceBase, IConfigurationAppService
{
    public async Task ChangeUiTheme(ChangeUiThemeInput input)
    {
        await SettingManager.ChangeSettingForUserAsync(AbpSession.ToUserIdentifier(), AppSettingNames.UiTheme, input.Theme);
    }
}
