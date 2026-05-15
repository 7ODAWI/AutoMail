using System.Net;
using GitHubEmailScraper.Models;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Infrastructure;

/// <summary>
/// Round-robin proxy rotation. Returns null (no proxy) when proxy support is disabled
/// or no proxies are configured.
/// </summary>
public sealed class ProxyRotator
{
    private readonly string[] _proxies;
    private readonly ILogger<ProxyRotator> _logger;
    private int _index = -1;

    public bool IsEnabled { get; }

    public ProxyRotator(AppSettings settings, ILogger<ProxyRotator> logger)
    {
        _logger = logger;
        IsEnabled = settings.Proxies.Enabled && settings.Proxies.List.Length > 0;
        _proxies = settings.Proxies.List;

        if (IsEnabled)
            _logger.LogInformation("Proxy rotation enabled with {Count} proxies", _proxies.Length);
    }

    /// <summary>
    /// Returns the next proxy in round-robin order, or null if proxies are disabled.
    /// </summary>
    public IWebProxy? GetNext()
    {
        if (!IsEnabled || _proxies.Length == 0)
            return null;

        var i = (int)((uint)Interlocked.Increment(ref _index) % (uint)_proxies.Length);
        var proxyUrl = _proxies[i];

        try
        {
            return new WebProxy(proxyUrl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid proxy URL: {Proxy}", proxyUrl);
            return null;
        }
    }
}
