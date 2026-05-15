using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace GitHubEmailScraper.Infrastructure;

/// <summary>
/// Configures the Polly resilience pipeline for the GitHub HttpClient.
///
/// Pipeline (outer → inner):
///   Timeout (30 s) wraps Retry (max 3, exponential + jitter, 2 s base).
///
/// Retried status codes: 5xx, 429 (secondary rate limit), 403 (primary / abuse).
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
                // Retry on network errors
                if (args.Outcome.Exception is HttpRequestException)
                    return ValueTask.FromResult(true);

                // Retry on specific HTTP status codes
                if (args.Outcome.Result is { } response)
                {
                    var code = (int)response.StatusCode;
                    return ValueTask.FromResult(
                        code >= 500 ||                         // server errors
                        response.StatusCode == (HttpStatusCode)429 ||  // secondary rate limit
                        response.StatusCode == HttpStatusCode.Forbidden // primary / abuse
                    );
                }
                return ValueTask.FromResult(false);
            },

            // For 429, prefer the Retry-After header; otherwise fall back to exponential backoff
            DelayGenerator = static args =>
            {
                if (args.Outcome.Result?.StatusCode == (HttpStatusCode)429)
                {
                    var delta = args.Outcome.Result.Headers.RetryAfter?.Delta;
                    if (delta.HasValue && delta.Value > TimeSpan.Zero)
                        return ValueTask.FromResult<TimeSpan?>(delta.Value);
                }
                return ValueTask.FromResult<TimeSpan?>(null); // use default exponential
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

        // Overall per-attempt timeout
        builder.AddTimeout(TimeSpan.FromSeconds(30));
    }
}
