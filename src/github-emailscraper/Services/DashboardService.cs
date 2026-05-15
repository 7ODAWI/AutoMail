using GitHubEmailScraper.Models;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Renders a live ASCII dashboard in the top N lines of the console every second.
/// Serilog log output scrolls below the dashboard area.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly PipelineStats _stats;
    private readonly DateTime _startTime = DateTime.UtcNow;

    private CancellationTokenSource? _cts;
    private Task? _renderTask;

    private const int DashboardLines = 14;
    private const int ConsoleWidth   = 66; // fixed inner width of the box

    public DashboardService(PipelineStats stats) => _stats = stats;

    public void Start(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            Console.Clear();
            Console.CursorVisible = false;
        }
        catch { /* non-interactive terminals may throw */ }

        // Reserve lines at the top
        for (var i = 0; i < DashboardLines; i++)
            Console.WriteLine();

        _renderTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    Render();
                    await Task.Delay(1000, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch { /* swallow render errors */ }
            }
            Render(); // final snapshot
        }, CancellationToken.None);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _renderTask?.Wait(TimeSpan.FromSeconds(3)); } catch { }

        try { Console.CursorVisible = true; } catch { }
        Render();
    }

    private void Render()
    {
        try
        {
            var elapsed = DateTime.UtcNow - _startTime;
            Console.SetCursorPosition(0, 0);

            WriteBoxLine("═", "╔", "╗");
            WriteRow($"  GitHub Email Scraper  │  Runtime: {FormatElapsed(elapsed),-12}");
            WriteBoxLine("═", "╠", "╣");
            WriteRow($"  Profiles Scanned : {_stats.ProfilesScanned,-9} │  Emails Found  : {_stats.EmailsFound,-9}");
            WriteRow($"  Requests/min     : {_stats.RequestsPerMinute,-9} │  Failures      : {_stats.Failures,-9}");
            WriteRow($"  Retries          : {_stats.Retries,-9} │  Dupes Skipped : {_stats.DuplicatesSkipped,-9}");
            WriteRow($"  Rate Limit Left  : {FormatRateLimit(),-40}");
            WriteBoxLine("═", "╠", "╣");
            var innerWidth = ConsoleWidth - 12;
            WriteRow($"  Query : {Clip(_stats.CurrentQuery, innerWidth).PadRight(innerWidth)}");
            WriteRow($"  User  : {Clip(_stats.CurrentUser,  innerWidth).PadRight(innerWidth)}");
            WriteBoxLine("═", "╠", "╣");
            WriteRow($"  Press Ctrl+C to stop gracefully.                    ");
            WriteBoxLine("═", "╚", "╝");
            Console.WriteLine(); // blank separator before log output
        }
        catch { /* non-interactive / redirected output */ }
    }

    private static void WriteBoxLine(string fill, string left, string right)
    {
        Console.WriteLine($"{left}{new string(fill[0], ConsoleWidth)}{right}");
    }

    private static void WriteRow(string content)
    {
        // Pad/truncate to fixed width inside the box borders
        var inner = content.Length <= ConsoleWidth
            ? content.PadRight(ConsoleWidth)
            : content[..ConsoleWidth];
        Console.WriteLine($"║{inner}║");
    }

    private string FormatRateLimit()
    {
        if (_stats.RateLimitRemaining < 0)
            return "Unknown (no authenticated request yet)";

        var reset     = _stats.RateLimitReset;
        var remaining = reset - DateTimeOffset.UtcNow;
        var timeStr   = remaining > TimeSpan.Zero
            ? $"resets in {(int)remaining.TotalSeconds}s"
            : "reset now";
        return $"{_stats.RateLimitRemaining} remaining ({timeStr})";
    }

    private static string FormatElapsed(TimeSpan ts) =>
        ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s"
            : $"{ts.Minutes:D2}m {ts.Seconds:D2}s";

    private static string Clip(string s, int max) =>
        s.Length <= max ? s : s[..(max - 3)] + "...";
}
