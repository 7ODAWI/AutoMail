using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using GitHubScraper.Models.Pipeline;
using GitHubScraper.Models.Settings;
using GitHubScraper.Pipeline;
using GitHubScraper.Services.Search;

namespace GitHubScraper.Services;

/// <summary>
/// Generates username yields from the GitHub REST Search API using a cartesian product
/// of Keywords × Locations. Respects checkpoint state (skips completed queries/pages).
/// Each operation provides its own PAT which is set per-request on the Authorization header.
/// </summary>
public sealed class GitHubSearchService : IGitHubSearchService
{
    private readonly HttpClient _http;
    private readonly GitHubOptions _options;
    private readonly IGitHubSearchQueryBuilder _queryBuilder;
    private readonly string? _pat;
    private readonly string[] _keywords;
    private readonly string[] _locations;
    private readonly int _minFollowers;
    private readonly CheckpointState _checkpoint;
    private readonly PipelineStats _stats;
    private readonly ILogger<GitHubSearchService> _logger;
    // process-wide rate limit tracker
    private static DateTimeOffset s_nextRequestTime = DateTimeOffset.MinValue;
    private static readonly object s_timeLock = new();

    public GitHubSearchService(
        IHttpClientFactory httpFactory,
        GitHubOptions options,
        IGitHubSearchQueryBuilder queryBuilder,
        string? pat,
        string[] keywords,
        string[] locations,
        int minFollowers,
        CheckpointState checkpoint,
        PipelineStats stats,
        ILogger<GitHubSearchService> logger)
    {
        _http = httpFactory.CreateClient("Web");
        _options = options;
        _queryBuilder = queryBuilder;
        _pat = pat;
        _keywords = keywords;
        _locations = locations;
        _minFollowers = minFollowers;
        _checkpoint = checkpoint;
        _stats = stats;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> SearchUsernamesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var builtQuery in BuildQueries())
        {
            if (ct.IsCancellationRequested) yield break;

            var query = builtQuery.Query;

            if (_checkpoint.IsQueryCompleted(query))
            {
                _logger.LogDebug("Skipping completed query: {Query}", query);
                continue;
            }

            _stats.CurrentQuery = query;
            _logger.LogInformation("Searching: {Query}", query);

            var startPage = Math.Max(1, _checkpoint.GetLastPage(query));
            var queryFailed = false;

            for (var page = startPage; page <= 100; page++)
            {
                if (ct.IsCancellationRequested) yield break;

                _checkpoint.SetLastPage(query, page);

                GitHubWebSearchPayload? webResult = null;
                try
                {
                    var url = _queryBuilder.BuildWebSearchUrl(_options.WebBaseUrl, query, page, "users");
                    var referrer = $"{_options.WebBaseUrl.TrimEnd('/')}/search?q={Uri.EscapeDataString(query)}&type=users&p={page}";

                    _logger.LogDebug("Search query AST:\n{Tree}", builtQuery.DebugTree);

                    using var response = await SendWithRetryAsync(url, referrer, ct);
                    webResult = await response.Content.ReadFromJsonAsync<GitHubWebSearchPayload>(cancellationToken: ct);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex,
                        "Search failed for query={Query} page={Page} — will retry on next run",
                        query, page);
                    _stats.IncrementFailures();
                    queryFailed = true;
                    break;
                }

                var users = webResult?.Payload?.Results;
                if (users is null || users.Length == 0)
                    break;
                _logger.LogInformation("'{Query}' page {Page}: {Count}", query, page, users.Length);

                foreach (var item in users)
                {
                    if (ct.IsCancellationRequested) yield break;
                    // prefer display_login when present
                    var uname = item.DisplayLogin ?? item.Login;
                    if (!string.IsNullOrWhiteSpace(uname))
                        yield return uname!;
                }

                // stop when we've reached last page as reported by payload
                if (webResult?.Payload != null && webResult.Payload.PageCount > 0 && page >= webResult.Payload.PageCount)
                    break;
            }

            if (!queryFailed)
                _checkpoint.MarkQueryCompleted(query);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(string url, string referrer, CancellationToken ct)
    {
        const int maxAttempts = 5;
        var rnd = new Random();
        

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                // if a global pause is active, wait until it's over
                DateTimeOffset now = DateTimeOffset.UtcNow;
                DateTimeOffset waitUntil;
                lock (s_timeLock)
                {
                    waitUntil = s_nextRequestTime;
                }

                if (waitUntil > now)
                {
                    var waitMs = (int)Math.Min((waitUntil - now).TotalMilliseconds, int.MaxValue);
                    _logger.LogInformation("Global rate-limit pause active. Waiting {Ms}ms before request.", waitMs);
                    await Task.Delay(waitMs, ct);
                }

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("Accept", "application/json");
                req.Headers.Referrer = new Uri(referrer);

                response = await _http.SendAsync(req, ct);

                if (response.IsSuccessStatusCode)
                    return response;

                if ((int)response.StatusCode == 429)
                {
                    // read Retry-After header if present
                    var retryAfter = 0;
                    if (response.Headers.RetryAfter != null)
                    {
                        if (response.Headers.RetryAfter.Delta.HasValue)
                            retryAfter = (int)response.Headers.RetryAfter.Delta.Value.TotalSeconds;
                        else if (response.Headers.RetryAfter.Date.HasValue)
                        {
                            var delta = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                            retryAfter = (int)Math.Max(0, delta.TotalSeconds);
                        }
                    }

                    // exponential backoff base (seconds)
                    var backoff = Math.Pow(2, attempt - 1);
                    var jitter = rnd.Next(250, 1000) / 1000.0; // 0.25-1.0s
                    var delaySeconds = Math.Max(retryAfter, (int)Math.Ceiling(backoff + jitter));

                    _logger.LogWarning("Received 429 Too Many Requests. Attempt {Attempt}/{MaxAttempts}. Applying global pause {Delay}s.", attempt, maxAttempts, delaySeconds);

                    // set global next allowed request time
                    lock (s_timeLock)
                    {
                        var candidate = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
                        if (candidate > s_nextRequestTime)
                            s_nextRequestTime = candidate;
                    }

                    response.Dispose();
                    if (attempt == maxAttempts)
                        break;

                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), ct);
                    continue;
                }

                // for other non-success statuses, throw to be handled by caller
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (OperationCanceledException)
            {
                response?.Dispose();
                throw;
            }
            catch
            {
                response?.Dispose();
                if (attempt == maxAttempts)
                    throw;

                var wait = (int)Math.Pow(2, attempt);
                await Task.Delay(TimeSpan.FromSeconds(wait), ct);
            }
        }

        throw new HttpRequestException("Exceeded retry attempts due to rate limiting.");
    }

    private IEnumerable<GitHubBuiltQuery> BuildQueries()
    {
        if (_keywords.Length == 0 && _locations.Length == 0)
        {
            _logger.LogWarning("No Keywords or Locations configured — nothing to search.");
            yield break;
        }

        var kws = _keywords.Length == 0 ? new[] { string.Empty } : _keywords;
        var locs = _locations.Length == 0 ? new[] { string.Empty } : _locations;

        foreach (var keyword in kws)
        foreach (var location in locs)
        {
            var qualifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(location))
                qualifiers["location"] = location;
            if (_minFollowers > 0)
                qualifiers["followers"] = $">={_minFollowers}";

            var expression = NormalizeKeywordExpression(keyword);

            GitHubBuiltQuery built;
            try
            {
                built = _queryBuilder.Build(new GitHubQueryBuildRequest(expression, qualifiers));
            }
            catch (GitHubSearchQueryException ex)
            {
                _logger.LogWarning(ex,
                    "Skipping malformed query. Keyword='{Keyword}' Location='{Location}'",
                    keyword,
                    location);
                _stats.IncrementFailures();
                continue;
            }

            yield return built;
        }
    }

    private static string? NormalizeKeywordExpression(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return null;

        var trimmed = keyword.Trim();
        if (LooksAdvancedExpression(trimmed))
            return trimmed;

        // Backward-compatible behavior: plain multi-word tags remain exact phrase searches.
        if (trimmed.Contains(' ', StringComparison.Ordinal))
            return '"' + trimmed + '"';

        return trimmed;
    }

    private static bool LooksAdvancedExpression(string value)
    {
        if (value.Contains('(', StringComparison.Ordinal)
            || value.Contains(')', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal))
        {
            return true;
        }

        return value.Contains(" AND ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase)
            || value.Contains(" OR ", StringComparison.OrdinalIgnoreCase)
            || value.Contains(" NOT ", StringComparison.OrdinalIgnoreCase);
    }
}
