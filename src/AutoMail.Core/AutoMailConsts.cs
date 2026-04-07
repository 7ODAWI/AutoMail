using AutoMail.Debugging;

namespace AutoMail;

public class AutoMailConsts
{
    public const string LocalizationSourceName = "AutoMail";

    public const string ConnectionStringName = "Default";

    public const bool MultiTenancyEnabled = true;


    /// <summary>
    /// Default pass phrase for SimpleStringCipher decrypt/encrypt operations
    /// </summary>
    public static readonly string DefaultPassPhrase =
        DebugHelper.IsDebug ? "gsKxGZ012HLL3MI5" : "da5075029a6146009705fd98e2bcaa0a";
}
