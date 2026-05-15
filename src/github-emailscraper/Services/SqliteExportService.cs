using GitHubEmailScraper.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Optional SQLite export. Performs an UPSERT on Username, preferring non-empty
/// values for Email / EmailSource / EmailConfidence on conflict.
/// No-ops entirely when EnableSqlite = false.
/// </summary>
public sealed class SqliteExportService : ISqliteExportService
{
    private readonly string _dbPath;
    private readonly bool   _enabled;
    private readonly ILogger<SqliteExportService> _logger;

    private SqliteConnection? _conn;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteExportService(AppSettings settings, ILogger<SqliteExportService> logger)
    {
        _dbPath  = settings.Output.SqlitePath;
        _enabled = settings.Output.EnableSqlite;
        _logger  = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (!_enabled) return;

        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _conn = new SqliteConnection($"Data Source={_dbPath}");
        await _conn.OpenAsync(ct);

        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Developers (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                Username         TEXT    NOT NULL UNIQUE,
                Name             TEXT,
                Location         TEXT,
                Bio              TEXT,
                Email            TEXT,
                EmailSource      TEXT,
                EmailConfidence  TEXT,
                Website          TEXT,
                Followers        INTEGER DEFAULT 0,
                Repos            INTEGER DEFAULT 0,
                ProfileUrl       TEXT,
                FoundAtUtc       TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_dev_username ON Developers(Username);
            CREATE INDEX IF NOT EXISTS idx_dev_email    ON Developers(Email);
            """;

        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogInformation("SQLite initialized: {Path}", _dbPath);
    }

    public async Task UpsertAsync(DeveloperRecord record, CancellationToken ct = default)
    {
        if (!_enabled || _conn is null) return;

        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO Developers
                    (Username, Name, Location, Bio, Email, EmailSource, EmailConfidence,
                     Website, Followers, Repos, ProfileUrl, FoundAtUtc)
                VALUES
                    ($username, $name, $location, $bio, $email, $emailSource, $emailConfidence,
                     $website, $followers, $repos, $profileUrl, $foundAtUtc)
                ON CONFLICT(Username) DO UPDATE SET
                    Email           = COALESCE(NULLIF($email,''),           Email),
                    EmailSource     = COALESCE(NULLIF($emailSource,''),     EmailSource),
                    EmailConfidence = COALESCE(NULLIF($emailConfidence,''), EmailConfidence),
                    Name            = COALESCE(NULLIF($name,''),            Name),
                    Location        = COALESCE(NULLIF($location,''),        Location),
                    Followers       = MAX(Followers, $followers),
                    Repos           = MAX(Repos, $repos);
                """;

            cmd.Parameters.AddWithValue("$username",        record.Username);
            cmd.Parameters.AddWithValue("$name",            record.Name);
            cmd.Parameters.AddWithValue("$location",        record.Location);
            cmd.Parameters.AddWithValue("$bio",             record.Bio);
            cmd.Parameters.AddWithValue("$email",           record.Email);
            cmd.Parameters.AddWithValue("$emailSource",     record.EmailSource);
            cmd.Parameters.AddWithValue("$emailConfidence", record.EmailConfidence);
            cmd.Parameters.AddWithValue("$website",         record.Website);
            cmd.Parameters.AddWithValue("$followers",       record.Followers);
            cmd.Parameters.AddWithValue("$repos",           record.Repos);
            cmd.Parameters.AddWithValue("$profileUrl",      record.ProfileUrl);
            cmd.Parameters.AddWithValue("$foundAtUtc",      record.FoundAtUtc);

            await cmd.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_conn is not null) await _conn.DisposeAsync();
    }
}
