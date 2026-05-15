namespace GitHubEmailScraper.Models;

/// <summary>
/// Thread-safe pipeline metrics. All counters use Interlocked operations.
/// </summary>
public sealed class PipelineStats
{
    // Counters (Interlocked)
    private long _profilesScanned;
    private long _emailsFound;
    private long _requestCount;
    private long _failures;
    private long _retries;
    private long _duplicatesSkipped;

    // Rate limit state
    private int _rateLimitRemaining = -1;
    private long _rateLimitResetEpoch;

    // Sliding request window for req/min
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _tsLock = new();

    // Volatile strings for dashboard display (last-write wins, no strict ordering needed)
    private volatile string _currentQuery = string.Empty;
    private volatile string _currentUser = string.Empty;

    public string CurrentQuery
    {
        get => _currentQuery;
        set => _currentQuery = value;
    }

    public string CurrentUser
    {
        get => _currentUser;
        set => _currentUser = value;
    }

    // --- Read properties ---
    public long ProfilesScanned    => Interlocked.Read(ref _profilesScanned);
    public long EmailsFound        => Interlocked.Read(ref _emailsFound);
    public long RequestCount       => Interlocked.Read(ref _requestCount);
    public long Failures           => Interlocked.Read(ref _failures);
    public long Retries            => Interlocked.Read(ref _retries);
    public long DuplicatesSkipped  => Interlocked.Read(ref _duplicatesSkipped);

    public int RateLimitRemaining => Volatile.Read(ref _rateLimitRemaining);

    public DateTimeOffset RateLimitReset =>
        DateTimeOffset.FromUnixTimeSeconds(Interlocked.Read(ref _rateLimitResetEpoch));

    // --- Mutators ---
    public void IncrementProfilesScanned()   => Interlocked.Increment(ref _profilesScanned);
    public void IncrementEmailsFound()       => Interlocked.Increment(ref _emailsFound);
    public void IncrementRequestCount()      => Interlocked.Increment(ref _requestCount);
    public void IncrementFailures()          => Interlocked.Increment(ref _failures);
    public void IncrementRetries()           => Interlocked.Increment(ref _retries);
    public void IncrementDuplicatesSkipped() => Interlocked.Increment(ref _duplicatesSkipped);

    public void UpdateRateLimit(int remaining, long resetEpoch)
    {
        Interlocked.Exchange(ref _rateLimitRemaining, remaining);
        Interlocked.Exchange(ref _rateLimitResetEpoch, resetEpoch);
    }

    /// <summary>Records a request and updates the sliding req/min window.</summary>
    public void RecordRequest()
    {
        IncrementRequestCount();
        var now = DateTime.UtcNow;
        lock (_tsLock)
        {
            _requestTimestamps.Enqueue(now);
            // Purge entries older than 60 seconds
            while (_requestTimestamps.Count > 0 &&
                   (now - _requestTimestamps.Peek()).TotalSeconds > 60)
            {
                _requestTimestamps.Dequeue();
            }
        }
    }

    /// <summary>Approximate requests in the last 60 seconds.</summary>
    public int RequestsPerMinute
    {
        get
        {
            lock (_tsLock)
                return _requestTimestamps.Count;
        }
    }
}
