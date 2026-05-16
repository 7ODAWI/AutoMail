using System.Collections.Concurrent;
using System.Text.Json;

namespace GitHubScraper.Services;

/// <summary>
/// In-memory checkpoint state for a single scraping operation.
/// Loaded from the DB (DeveloperResults) at operation start.
/// Serialized to/from JSON for storage in ScrapingOperation.CheckpointJson.
/// No file I/O — all state is in memory and backed by the database.
/// </summary>
public sealed class CheckpointState
{
    // O(1) dedup — loaded from DB on start, updated as pipeline processes usernames
    private readonly ConcurrentDictionary<string, byte> _processed = new(StringComparer.OrdinalIgnoreCase);

    // Query-level resume state
    private readonly object _lock = new();
    private readonly HashSet<string> _completedQueries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _queryPages = new(StringComparer.Ordinal);

    public int TotalProcessed => _processed.Count;

    // ── Dedup ──────────────────────────────────────────────────────────────

    public void SeedProcessed(IEnumerable<string> usernames)
    {
        foreach (var u in usernames)
            _processed.TryAdd(u, 0);
    }

    public bool IsProcessed(string username) => _processed.ContainsKey(username);

    public void MarkProcessed(string username) => _processed.TryAdd(username, 0);

    // ── Query resume ───────────────────────────────────────────────────────

    public bool IsQueryCompleted(string query)
    {
        lock (_lock) return _completedQueries.Contains(query);
    }

    public void MarkQueryCompleted(string query)
    {
        lock (_lock) _completedQueries.Add(query);
    }

    public int GetLastPage(string query)
    {
        lock (_lock) return _queryPages.GetValueOrDefault(query, 1);
    }

    public void SetLastPage(string query, int page)
    {
        lock (_lock) _queryPages[query] = page;
    }

    // ── Serialization (stored in ScrapingOperation.CheckpointJson) ─────────

    public string Serialize()
    {
        lock (_lock)
        {
            var data = new CheckpointSnapshot
            {
                CompletedQueries = _completedQueries.ToList(),
                QueryPages = new Dictionary<string, int>(_queryPages)
            };
            return JsonSerializer.Serialize(data);
        }
    }

    public void Load(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var data = JsonSerializer.Deserialize<CheckpointSnapshot>(json);
            if (data is null) return;
            lock (_lock)
            {
                foreach (var q in data.CompletedQueries)
                    _completedQueries.Add(q);
                foreach (var kv in data.QueryPages)
                    _queryPages[kv.Key] = kv.Value;
            }
        }
        catch { /* corrupt JSON — start fresh */ }
    }

    private sealed class CheckpointSnapshot
    {
        public List<string> CompletedQueries { get; init; } = new();
        public Dictionary<string, int> QueryPages { get; init; } = new();
    }
}
