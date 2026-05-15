using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using GitHubEmailScraper.Models;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Generates username yields from the GitHub REST Search API
/// (https://api.github.com/search/users) using a cartesian product of Keywords × Locations.
/// Works without a PAT at 10 req/min; set PersonalAccessToken for 30 req/min.
/// Pagination is capped at 10 pages × 100 = 1 000 results per query (GitHub hard limit).
/// Respects checkpoint state (skips completed queries/pages).
/// </summary>
public sealed class GitHubSearchService : IGitHubSearchService
{
    private readonly HttpClient _http;
    private readonly AppSettings _settings;
    private readonly ICheckpointService _checkpoint;
    private readonly PipelineStats _stats;
    private readonly ILogger<GitHubSearchService> _logger;

    public GitHubSearchService(
        IHttpClientFactory httpFactory,
        AppSettings settings,
        ICheckpointService checkpoint,
        PipelineStats stats,
        ILogger<GitHubSearchService> logger)
    {
        _http = httpFactory.CreateClient("GitHub");
        _settings = settings;
        _checkpoint = checkpoint;
        _stats = stats;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> SearchUsernamesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var query in BuildQueries())
        {
            if (ct.IsCancellationRequested) yield break;

            if (_checkpoint.IsQueryCompleted(query))
            {
                _logger.LogDebug("Skipping completed query: {Query}", query);
                continue;
            }

            _stats.CurrentQuery = query;
            _logger.LogInformation("Searching: {Query}", query);

            var startPage = Math.Max(1, _checkpoint.GetLastPage(query));
            var queryFailed = false;

            for (var page = startPage; page <= 10; page++)
            {
                if (ct.IsCancellationRequested) yield break;

                _checkpoint.SetLastPage(query, page);

                GitHubSearchResult? result = null;
                try
                {
                    var url = BuildSearchUrl(query, page);
                    result = await _http.GetFromJsonAsync<GitHubSearchResult>(url, ct);
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

                var users = result?.Items;
                if (users is null || users.Length == 0)
                    break;

                _logger.LogInformation(
                    "'{Query}' page {Page}: {Count}/{Total}",
                    query, page, users.Length, result!.TotalCount);

                foreach (var item in users)
                {
                    if (ct.IsCancellationRequested) yield break;
                    yield return item.Login;
                }

                // Stop when we've reached the last page GitHub will serve (hard cap 1 000)
                var fetched = (page - startPage + 1) * 100;
                if (users.Length < 100 || fetched >= Math.Min(result!.TotalCount, 1000))
                    break;
            }

            // Only mark completed if the query finished without an HTTP error.
            // Failed queries are retried on the next run.
            if (!queryFailed)
                _checkpoint.MarkQueryCompleted(query);
        }
    }

    // Cartesian product: Keywords × Locations
    private IEnumerable<string> BuildQueries()
    {
        var keywords = _settings.Search.Keywords;
        var locations = _settings.Search.Locations;

        if (keywords.Length == 0 || locations.Length == 0)
        {
            _logger.LogWarning("No Keywords or Locations configured — nothing to search.");
            yield break;
        }

        foreach (var keyword in keywords)
            foreach (var location in locations)
            {
                var sb = new StringBuilder();
                sb.Append('"').Append(keyword).Append('"');
                sb.Append(" location:\"").Append(location).Append('"');
                if (_settings.Search.MinFollowers > 0)
                    sb.Append($" followers:>={_settings.Search.MinFollowers}");
                yield return sb.ToString();
            }
    }

    private static string BuildSearchUrl(string query, int page) =>
        $"/search/users?q={Uri.EscapeDataString(query)}&per_page=100&page={page}&sort=joined&order=desc";
}
