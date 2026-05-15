namespace GitHubEmailScraper.Infrastructure;

/// <summary>
/// Round-robin rotation of User-Agent strings to reduce fingerprinting.
/// Thread-safe via Interlocked.
/// </summary>
public sealed class UserAgentRotator
{
    private static readonly string[] Agents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_4_1) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4.1 Safari/605.1.15",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0",
        "Mozilla/5.0 (compatible; GitHubEmailScraper/1.0; +mailto:dev@example.com)",
        "GitHubEmailScraper/1.0 (Research Tool; .NET 8)"
    ];

    private int _index = -1;

    public string GetNext()
    {
        var i = (int)((uint)Interlocked.Increment(ref _index) % (uint)Agents.Length);
        return Agents[i];
    }
}
