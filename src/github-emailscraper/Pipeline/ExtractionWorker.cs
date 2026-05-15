using System.Threading.Channels;
using GitHubEmailScraper.Models;
using GitHubEmailScraper.Services;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Pipeline;

/// <summary>
/// One of N concurrent consumers from the profile channel.
/// Scrapes HTML page(s) for email addresses and writes DeveloperRecord rows
/// to the record channel — one row per email found, or one empty-email row if
/// no emails were discovered (so the profile still appears in the CSV).
/// </summary>
public sealed class ExtractionWorker
{
    private readonly IEmailExtractionService  _emailService;
    private readonly PipelineStats            _stats;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(
        IEmailExtractionService   emailService,
        PipelineStats             stats,
        ILogger<ExtractionWorker> logger)
    {
        _emailService = emailService;
        _stats        = stats;
        _logger       = logger;
    }

    public async Task RunAsync(
        ChannelReader<GitHubUser>       profileReader,
        ChannelWriter<DeveloperRecord>  recordWriter,
        int workerId,
        CancellationToken ct)
    {
        _logger.LogInformation("Extraction worker {Id} started.", workerId);

        try
        {
            await foreach (var user in profileReader.ReadAllAsync(ct))
            {
                if (ct.IsCancellationRequested) break;

                var emails = await _emailService.ExtractAsync(user, ct);

                foreach (var email in emails)
                {
                    var record = BuildRecord(user, email);
                    await recordWriter.WriteAsync(record, ct);
                    _logger.LogDebug("Email: {Email} [{Source}] — {User}",
                        email.Email, email.Source, user.Login);
                }
                // Users without a public email are silently skipped
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Extraction worker {Id} cancelled.", workerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction worker {Id} encountered an unhandled error.", workerId);
        }

        _logger.LogInformation("Extraction worker {Id} completed.", workerId);
    }

    private static DeveloperRecord BuildRecord(GitHubUser user, EmailResult? email) => new()
    {
        Username        = user.Login,
        Name            = user.Name            ?? string.Empty,
        Location        = user.Location        ?? string.Empty,
        Bio             = user.Bio             ?? string.Empty,
        Email           = email?.Email         ?? string.Empty,
        EmailSource     = email?.Source        ?? string.Empty,
        EmailConfidence = email?.Confidence.ToString() ?? string.Empty,
        Website         = user.Blog            ?? string.Empty,
        Followers       = user.Followers,
        Repos           = user.PublicRepos,
        ProfileUrl      = user.HtmlUrl,
        FoundAtUtc      = DateTime.UtcNow.ToString("o")
    };
}
