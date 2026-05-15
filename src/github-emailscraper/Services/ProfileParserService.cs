using System.Net;
using System.Text.RegularExpressions;
using GitHubEmailScraper.Models;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Fetches a user's public profile page from https://github.com/{username}
/// and extracts profile data (email, name, location, etc.) from the HTML.
/// Returns null for 404 or when the page cannot be fetched.
/// Users without a public email will have a null Email field and will be
/// silently skipped by the extraction pipeline.
/// </summary>
public sealed class ProfileParserService : IProfileParserService
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;
    private readonly PipelineStats _stats;
    private readonly ILogger<ProfileParserService> _logger;

    public ProfileParserService(
        IHttpClientFactory factory,
        AppSettings settings,
        PipelineStats stats,
        ILogger<ProfileParserService> logger)
    {
        _http = factory.CreateClient("Web");
        _settings = settings;
        _stats = stats;
        _logger = logger;
    }

    // Max attempts before giving up on a 429-throttled profile.
    private const int MaxRateLimitRetries = 3;

    public async Task<GitHubUser?> GetUserAsync(string username, CancellationToken ct = default)
    {
        _stats.CurrentUser = username;
        var profileUrl = $"{_settings.GitHub.WebBaseUrl.TrimEnd('/')}/{username}";

        for (int attempt = 0; attempt < MaxRateLimitRetries; attempt++)
        {
            try
            {
                using var response = await _http.SendAsync(
                    new HttpRequestMessage(HttpMethod.Get, profileUrl), ct);

                // Permanent absence — no retry
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogDebug("Profile page not found: {Username}", username);
                    return null;
                }

                // Transient rate-limit — pause then retry
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var wait = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromSeconds(30 << attempt); // 30 s → 60 s → 120 s
                    _logger.LogInformation(
                        "Rate limited by GitHub web. Pausing {Sec}s before retrying {Username} (attempt {N}/{Max})...",
                        (int)wait.TotalSeconds, username, attempt + 1, MaxRateLimitRetries);
                    await Task.Delay(wait, ct);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync(ct);
                _stats.IncrementProfilesScanned();

                if (_settings.GitHub.WebPageDelayMs > 0)
                    await Task.Delay(_settings.GitHub.WebPageDelayMs, ct);

                return ParseProfile(username, profileUrl, html);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch profile page for {Username}", username);
                _stats.IncrementFailures();
                return null;
            }
        }

        _logger.LogWarning("Giving up on {Username} after {Max} rate-limit retries",
            username, MaxRateLimitRetries);
        _stats.IncrementFailures();
        return null;
    }

    private static GitHubUser ParseProfile(string username, string profileUrl, string html)
    {
        return new GitHubUser
        {
            Login   = username,
            HtmlUrl = profileUrl,
            Email   = ExtractEmail(html),
            Name    = ExtractName(html),
            Location = ExtractLocation(html),
            Company  = ExtractCompany(html),
            Blog     = ExtractBlog(html),
            Bio      = ExtractBio(html),
        };
    }

    // Finds the first mailto: link — GitHub only renders one when the user has a public email.
    private static string? ExtractEmail(string html)
    {
        var m = Regex.Match(html, @"href=""mailto:([^""]+)""", RegexOptions.IgnoreCase);
        return m.Success ? Uri.UnescapeDataString(m.Groups[1].Value.Trim()) : null;
    }

    // Page title format: "Full Name (login) · GitHub"
    private static string? ExtractName(string html)
    {
        var m = Regex.Match(html, @"<title>\s*(.+?)\s*\(", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var candidate = m.Groups[1].Value.Trim();
            if (candidate.Length > 0) return candidate;
        }
        // Fallback: itemprop="name"
        m = Regex.Match(html, @"itemprop=""name""[^>]*>\s*([^<\r\n]+?)\s*<",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractLocation(string html)
    {
        // Modern GitHub renders: aria-label="Home location: City, Country"
        var m = Regex.Match(html, @"aria-label=""Home location:\s*([^""]+)""",
            RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Trim();

        // Fallback: itemprop="homeLocation" … <span>…</span>
        m = Regex.Match(html,
            @"itemprop=""homeLocation""[^>]*>(?:(?!<li).){0,400}<span[^>]*>\s*([^<]+?)\s*</span>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractCompany(string html)
    {
        var m = Regex.Match(html, @"aria-label=""Works at:\s*([^""]+)""",
            RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups[1].Value.Trim();

        m = Regex.Match(html,
            @"itemprop=""worksFor""[^>]*>(?:(?!<li).){0,400}<span[^>]*>\s*([^<]+?)\s*</span>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractBlog(string html)
    {
        var m = Regex.Match(html,
            @"itemprop=""url""[^>]*href=""([^""]+)""", RegexOptions.IgnoreCase);
        if (!m.Success)
            m = Regex.Match(html,
                @"href=""([^""]+)""[^>]*itemprop=""url""", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? ExtractBio(string html)
    {
        var m = Regex.Match(html,
            @"data-bio-text[^>]*>\s*([^<\r\n]+?)\s*</",
            RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
