using GitHubScraper.Data;
using GitHubScraper.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace GitHubScraper.Services;

/// <summary>
/// Thread-safe DB writer for DeveloperResult rows.
/// Uses IServiceScopeFactory to create a fresh DbContext scope per batch write
/// (EF Core DbContext is not thread-safe and must not be shared across threads).
/// SemaphoreSlim(1,1) ensures only one write happens at a time per operation.
/// </summary>
public sealed class DbWriterService : IDbWriterService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<DbWriterService> _logger;

    public DbWriterService(IServiceScopeFactory scopeFactory, ILogger<DbWriterService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task WriteAsync(DeveloperResult record, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Upsert: skip if username already exists for this operation (unique index guard)
            var exists = await db.DeveloperResults.AnyAsync(
                r => r.OperationId == record.OperationId && r.Username == record.Username, ct);

            if (!exists)
            {
                db.DeveloperResults.Add(record);
                await db.SaveChangesAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: another write beat us — safe to ignore
            _logger.LogDebug("Duplicate record skipped for {Username} op {Op}",
                record.Username, record.OperationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write DeveloperResult for {Username}", record.Username);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true ||
        ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
}
