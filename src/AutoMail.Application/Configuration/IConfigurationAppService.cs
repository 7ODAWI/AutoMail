using AutoMail.Configuration.Dto;
using System.Threading.Tasks;

namespace AutoMail.Configuration;

public interface IConfigurationAppService
{
    Task ChangeUiTheme(ChangeUiThemeInput input);
}
