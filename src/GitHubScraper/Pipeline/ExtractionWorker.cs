using System.Threading.Channels;
using GitHubScraper.Models.Entities;
using GitHubScraper.Models.Pipeline;
using GitHubScraper.Services;

namespace GitHubScraper.Pipeline;

/// <summary>
/// One of N concurrent consumers from the profile channel.
/// Extracts emails from GitHubUser profiles and writes DeveloperResult rows
/// to the record channel.
/// </summary>
public sealed class ExtractionWorker
{
    private readonly IEmailExtractionService _emailService;
    private readonly PipelineStats _stats;
    private readonly ILogger<ExtractionWorker> _logger;

    public ExtractionWorker(
        IEmailExtractionService emailService,
        PipelineStats stats,
        ILogger<ExtractionWorker> logger)
    {
        _emailService = emailService;
        _stats = stats;
        _logger = logger;
    }

    public async Task RunAsync(
        ChannelReader<GitHubUser> profileReader,
        ChannelWriter<DeveloperResult> recordWriter,
        int workerId,
        Guid operationId,
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
                    var record = BuildRecord(user, email, operationId);
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

    private static DeveloperResult BuildRecord(GitHubUser user, EmailResult email, Guid operationId) => new()
    {
        OperationId     = operationId,
        Username        = user.Login,
        Name            = user.Name,
        Location        = user.Location,
        Bio             = user.Bio,
        Email           = email.Email,
        EmailSource     = email.Source,
        EmailConfidence = email.Confidence.ToString(),
        Website         = user.Blog,
        Followers       = user.Followers,
        Repos           = user.PublicRepos,
        ProfileUrl      = user.HtmlUrl,
        FoundAtUtc      = DateTime.UtcNow
    };
}
