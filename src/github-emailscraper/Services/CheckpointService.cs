using System.Collections.Concurrent;
using System.Text.Json;
using GitHubEmailScraper.Models;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

public sealed class CheckpointService : ICheckpointService
{
    private readonly string _checkpointPath;
    private readonly ILogger<CheckpointService> _logger;

    private CheckpointData _data = new();

    // ConcurrentDictionary<username, 0> — O(1) lookup, lock-free read
    private readonly ConcurrentDictionary<string, byte> _processedCache =
        new(StringComparer.OrdinalIgnoreCase);

    // Dedicated lock for mutations on _data.QueryPages / _data.CompletedQueries
    private readonly object _dataLock = new();

    private int _saveCounter;
    private const int AutoSaveEvery = 100;

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CheckpointService(AppSettings settings, ILogger<CheckpointService> logger)
    {
        _checkpointPath = settings.Output.CheckpointPath;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_checkpointPath))
        {
            _logger.LogInformation("No checkpoint found — starting fresh.");
            return;
        }

        try
        {
            await using var stream = File.OpenRead(_checkpointPath);
            _data = await JsonSerializer.DeserializeAsync<CheckpointData>(
                        stream, JsonOptions, ct)
                    ?? new CheckpointData();

            foreach (var u in _data.ProcessedUsernames)
                _processedCache.TryAdd(u, 0);

            _logger.LogInformation(
                "Checkpoint loaded: {Users} users, {Queries} queries completed.",
                _data.ProcessedUsernames.Count, _data.CompletedQueries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read checkpoint — starting fresh.");
            _data = new CheckpointData();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _saveLock.WaitAsync(ct);
        try
        {
            lock (_dataLock)
            {
                _data.ProcessedUsernames = new HashSet<string>(
                    _processedCache.Keys, StringComparer.OrdinalIgnoreCase);
                _data.TotalProcessed = _data.ProcessedUsernames.Count;
                _data.SavedAtUtc = DateTime.UtcNow;
            }

            var dir = Path.GetDirectoryName(_checkpointPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = _checkpointPath + ".tmp";
            await using (var stream = File.Create(tmp))
                await JsonSerializer.SerializeAsync(stream, _data, JsonOptions, ct);

            File.Move(tmp, _checkpointPath, overwrite: true);
            _logger.LogDebug("Checkpoint saved: {Count} users.", _data.ProcessedUsernames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save checkpoint.");
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public bool IsProcessed(string username) =>
        _processedCache.ContainsKey(username);

    public void MarkProcessed(string username)
    {
        _processedCache.TryAdd(username, 0);

        var count = Interlocked.Increment(ref _saveCounter);
        if (count % AutoSaveEvery == 0)
            _ = Task.Run(() => SaveAsync(CancellationToken.None));
    }

    public int GetLastPage(string query)
    {
        lock (_dataLock)
            return _data.QueryPages.TryGetValue(query, out var p) ? p : 1;
    }

    public void SetLastPage(string query, int page)
    {
        lock (_dataLock)
            _data.QueryPages[query] = page;
    }

    public void MarkQueryCompleted(string query)
    {
        lock (_dataLock)
            _data.CompletedQueries.Add(query);
    }

    public bool IsQueryCompleted(string query)
    {
        lock (_dataLock)
            return _data.CompletedQueries.Contains(query);
    }

    public int TotalProcessed => _processedCache.Count;
}
