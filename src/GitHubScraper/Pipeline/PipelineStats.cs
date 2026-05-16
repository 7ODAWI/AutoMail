namespace GitHubScraper.Pipeline;

/// <summary>
/// Thread-safe pipeline metrics. All counters use Interlocked operations.
/// </summary>
public sealed class PipelineStats
{
    private long _profilesScanned;
    private long _emailsFound;
    private long _requestCount;
    private long _failures;
    private long _retries;
    private long _duplicatesSkipped;

    private int _rateLimitRemaining = -1;
    private long _rateLimitResetEpoch;

    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _tsLock = new();

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

    public long ProfilesScanned   => Interlocked.Read(ref _profilesScanned);
    public long EmailsFound       => Interlocked.Read(ref _emailsFound);
    public long RequestCount      => Interlocked.Read(ref _requestCount);
    public long Failures          => Interlocked.Read(ref _failures);
    public long Retries           => Interlocked.Read(ref _retries);
    public long DuplicatesSkipped => Interlocked.Read(ref _duplicatesSkipped);

    public int RateLimitRemaining => Volatile.Read(ref _rateLimitRemaining);

    public DateTimeOffset RateLimitReset =>
        DateTimeOffset.FromUnixTimeSeconds(Interlocked.Read(ref _rateLimitResetEpoch));

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

    public void RecordRequest()
    {
        IncrementRequestCount();
        var now = DateTime.UtcNow;
        lock (_tsLock)
        {
            _requestTimestamps.Enqueue(now);
            while (_requestTimestamps.Count > 0 &&
                   (now - _requestTimestamps.Peek()).TotalSeconds > 60)
            {
                _requestTimestamps.Dequeue();
            }
        }
    }

    public int RequestsPerMinute
    {
        get
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-60);
            lock (_tsLock)
                return _requestTimestamps.Count(t => t >= cutoff);
        }
    }
}
