using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace GitHubScraper.Infrastructure.Http;

/// <summary>
/// Polly resilience pipeline for the GitHub HttpClient.
/// Pipeline (outer → inner): Timeout (30 s) wraps Retry (max 3, exponential + jitter, 2 s base).
/// Retried: 5xx, 429 (secondary rate limit), 403 (primary / abuse).
/// For 429, the Retry-After header is honoured as the delay.
/// </summary>
public static class RetryPolicies
{
    public static void ConfigureGitHubResilience(
        ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(2),

            ShouldHandle = static args =>
            {
                if (args.Outcome.Exception is HttpRequestException)
                    return ValueTask.FromResult(true);

                if (args.Outcome.Result is { } response)
                {
                    var code = (int)response.StatusCode;
                    return ValueTask.FromResult(
                        code >= 500 ||
                        response.StatusCode == (HttpStatusCode)429 ||
                        response.StatusCode == HttpStatusCode.Forbidden
                    );
                }
                return ValueTask.FromResult(false);
            },

            DelayGenerator = static args =>
            {
                if (args.Outcome.Result?.StatusCode == (HttpStatusCode)429)
                {
                    var delta = args.Outcome.Result.Headers.RetryAfter?.Delta;
                    if (delta.HasValue && delta.Value > TimeSpan.Zero)
                        return ValueTask.FromResult<TimeSpan?>(delta.Value);
                }
                return ValueTask.FromResult<TimeSpan?>(null);
            },

            OnRetry = static args =>
            {
                var statusCode = args.Outcome.Result?.StatusCode;
                Serilog.Log.Warning(
                    "Retry {Attempt}/{Max} after {Delay:g}. Status={Status} Exception={Ex}",
                    args.AttemptNumber + 1,
                    3,
                    args.RetryDelay,
                    statusCode?.ToString() ?? "n/a",
                    args.Outcome.Exception?.Message ?? string.Empty);
                return ValueTask.CompletedTask;
            }
        });

        builder.AddTimeout(TimeSpan.FromSeconds(30));
    }
}
