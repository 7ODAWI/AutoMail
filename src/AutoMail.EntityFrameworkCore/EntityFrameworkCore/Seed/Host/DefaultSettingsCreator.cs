using Abp.Configuration;
using Abp.Localization;
using Abp.MultiTenancy;
using Abp.Net.Mail;
using AutoMail.Configuration;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AutoMail.EntityFrameworkCore.Seed.Host;

public class DefaultSettingsCreator
{
    private readonly AutoMailDbContext _context;

    public DefaultSettingsCreator(AutoMailDbContext context)
    {
        _context = context;
    }

    public void Create()
    {
        int? tenantId = null;

        if (AutoMailConsts.MultiTenancyEnabled == false)
        {
            tenantId = MultiTenancyConsts.DefaultTenantId;
        }

        // Emailing
        AddSettingIfNotExists(EmailSettingNames.DefaultFromAddress, "admin@mydomain.com", tenantId);
        AddSettingIfNotExists(EmailSettingNames.DefaultFromDisplayName, "mydomain.com mailer", tenantId);
        AddSettingIfNotExists(EmailSettingNames.Smtp.Host, GmailSmtpDefaults.Host, tenantId);
        AddSettingIfNotExists(EmailSettingNames.Smtp.Port, GmailSmtpDefaults.Port.ToString(), tenantId);
        AddSettingIfNotExists(EmailSettingNames.Smtp.EnableSsl, GmailSmtpDefaults.EnableSsl.ToString().ToLowerInvariant(), tenantId);
        AddSettingIfNotExists(EmailSettingNames.Smtp.UseDefaultCredentials, false.ToString().ToLowerInvariant(), tenantId);
        AddSettingIfNotExists(EmailSettingNames.Smtp.UserName, string.Empty, tenantId);
        AddSettingIfNotExists(EmailSettingNames.Smtp.Password, string.Empty, tenantId);

        // Languages
        AddSettingIfNotExists(LocalizationSettingNames.DefaultLanguage, "en", tenantId);
    }

    private void AddSettingIfNotExists(string name, string value, int? tenantId = null)
    {
        if (_context.Settings.IgnoreQueryFilters().Any(s => s.Name == name && s.TenantId == tenantId && s.UserId == null))
        {
            return;
        }

        _context.Settings.Add(new Setting(tenantId, null, name, value));
        _context.SaveChanges();
    }
}
