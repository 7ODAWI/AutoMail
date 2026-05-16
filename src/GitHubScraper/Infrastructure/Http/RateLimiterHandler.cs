using GitHubScraper.Models.Settings;
using GitHubScraper.Pipeline;

namespace GitHubScraper.Infrastructure.Http;

/// <summary>
/// DelegatingHandler that:
///   1. Enforces a minimum SearchDelayMs gap between /search/* calls (thread-safe).
///   2. Records every request in PipelineStats (sliding req/min window).
///   3. Reads x-ratelimit-* response headers and updates PipelineStats.
///
/// 403 / 429 retries are handled by the outer Polly resilience pipeline.
/// This handler is intentionally stateless with respect to retry logic.
/// </summary>
public sealed class RateLimiterHandler : DelegatingHandler
{
    private readonly PipelineStats _stats;
    private readonly GitHubOptions _options;
    private readonly ILogger<RateLimiterHandler> _logger;

    private DateTime _lastSearchRequest = DateTime.MinValue;
    private readonly SemaphoreSlim _searchThrottle = new(1, 1);

    public RateLimiterHandler(
        PipelineStats stats,
        GitHubOptions options,
        ILogger<RateLimiterHandler> logger)
    {
        _stats = stats;
        _options = options;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsSearchRequest(request.RequestUri))
        {
            await _searchThrottle.WaitAsync(cancellationToken);
            try
            {
                var delayMs = _options.SearchDelayMs;
                var elapsed = (DateTime.UtcNow - _lastSearchRequest).TotalMilliseconds;
                if (elapsed < delayMs)
                {
                    var waitMs = (int)(delayMs - elapsed) + 50;
                    _logger.LogDebug("Search throttle: sleeping {Delay}ms", waitMs);
                    await Task.Delay(waitMs, cancellationToken);
                }
                _lastSearchRequest = DateTime.UtcNow;
            }
            finally
            {
                _searchThrottle.Release();
            }
        }

        _stats.RecordRequest();

        var response = await base.SendAsync(request, cancellationToken);

        UpdateRateLimitStats(response);

        return response;
    }

    private static bool IsSearchRequest(Uri? uri) =>
        uri?.AbsolutePath.StartsWith("/search", StringComparison.OrdinalIgnoreCase) == true;

    private void UpdateRateLimitStats(HttpResponseMessage response)
    {
        var remaining = ReadIntHeader(response, "x-ratelimit-remaining");
        var resetEpoch = ReadLongHeader(response, "x-ratelimit-reset");

        if (remaining >= 0)
        {
            _stats.UpdateRateLimit(remaining, resetEpoch > 0 ? resetEpoch : 0);
            _logger.LogDebug("Rate limit: {Remaining} remaining, resets at epoch {Reset}",
                remaining, resetEpoch);
        }
    }

    private static int ReadIntHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var vals) &&
            int.TryParse(vals.FirstOrDefault(), out var v))
            return v;
        return -1;
    }

    private static long ReadLongHeader(HttpResponseMessage response, string name)
    {
        if (response.Headers.TryGetValues(name, out var vals) &&
            long.TryParse(vals.FirstOrDefault(), out var v))
            return v;
        return 0;
    }
}
