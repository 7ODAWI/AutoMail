using GitHubScraper.Pipeline;

namespace GitHubScraper.Services;

/// <summary>Holds state for a currently-running operation.</summary>
public sealed class RunningOperation
{
    public CancellationTokenSource Cts { get; } = new();
    public PipelineStats Stats { get; } = new();
    public Task? PipelineTask { get; set; }
}
