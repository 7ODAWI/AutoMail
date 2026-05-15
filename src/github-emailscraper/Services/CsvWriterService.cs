using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using GitHubEmailScraper.Models;
using Microsoft.Extensions.Logging;

namespace GitHubEmailScraper.Services;

/// <summary>
/// Thread-safe CSV writer backed by a single CsvHelper instance protected by a semaphore.
/// Opens in append mode; writes the header row only for new / empty files.
/// Flushes to disk after every row so records survive unexpected termination.
/// </summary>
public sealed class CsvWriterService : ICsvWriterService
{
    private readonly string _csvPath;
    private readonly ILogger<CsvWriterService> _logger;

    private StreamWriter? _streamWriter;
    private CsvWriter?    _csvWriter;

    // CsvHelper is NOT thread-safe; guard with a binary semaphore
    private readonly SemaphoreSlim _lock = new(1, 1);

    public CsvWriterService(AppSettings settings, ILogger<CsvWriterService> logger)
    {
        _csvPath = settings.Output.CsvPath;
        _logger  = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_csvPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var isNewOrEmpty = !File.Exists(_csvPath) || new FileInfo(_csvPath).Length == 0;

        _streamWriter = new StreamWriter(
            _csvPath,
            append: true,
            Encoding.UTF8);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = isNewOrEmpty,
            NewLine         = "\r\n"
        };

        _csvWriter = new CsvWriter(_streamWriter, config);

        if (isNewOrEmpty)
        {
            _csvWriter.WriteHeader<DeveloperRecord>();
            await _csvWriter.NextRecordAsync();
            await _streamWriter.FlushAsync(ct);
        }

        _logger.LogInformation(
            "CSV initialized: {Path} (append={Append})", _csvPath, !isNewOrEmpty);
    }

    public async Task WriteAsync(DeveloperRecord record, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_csvWriter is null || _streamWriter is null)
                throw new InvalidOperationException("CsvWriterService.InitializeAsync was not called.");

            _csvWriter.WriteRecord(record);
            await _csvWriter.NextRecordAsync();
            await _streamWriter.FlushAsync(ct); // real-time flush
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_csvWriter    is not null) await _csvWriter.DisposeAsync();
        if (_streamWriter is not null) await _streamWriter.DisposeAsync();
    }
}
