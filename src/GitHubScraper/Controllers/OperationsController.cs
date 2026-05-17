using System.Globalization;
using System.Text.Json;
using CsvHelper;
using GitHubScraper.Data;
using GitHubScraper.Models.Entities;
using GitHubScraper.Models.Enums;
using GitHubScraper.Services;
using GitHubScraper.ViewModels.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GitHubScraper.Controllers;

public sealed class OperationsController : Controller
{
    private readonly OperationManager _manager;
    private readonly AppDbContext _db;

    public OperationsController(OperationManager manager, AppDbContext db)
    {
        _manager = manager;
        _db = db;
    }

    // GET /operations
    public async Task<IActionResult> Index()
    {
        var ops = await _manager.GetAllAsync();
        var vm = new OperationListViewModel
        {
            Operations = ops.Select(o => ToCard(o)).ToList()
        };
        return View(vm);
    }

    // GET /operations/create
    public IActionResult Create() => View(new CreateOperationViewModel());

    // POST /operations/create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateOperationViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .SelectMany(kvp => kvp.Value!.Errors.Select(e =>
                    $"{kvp.Key}: {(string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)}"))
                .ToArray();

            return View(vm);
        }

        var op = await _manager.CreateAsync(
            vm.Name,
            vm.GetKeywords(),
            vm.GetLocations(),
            vm.MinFollowers,
            string.IsNullOrWhiteSpace(vm.GitHubToken) ? null : vm.GitHubToken.Trim());

        if (vm.StartImmediately)
            await _manager.StartAsync(op.Id);

        return RedirectToAction(nameof(Detail), new { id = op.Id });
    }

    // GET /operations/{id}
    public async Task<IActionResult> Detail(Guid id, int page = 1)
    {
        const int pageSize = 50;
        var op = await _manager.GetAsync(id);
        if (op is null) return NotFound();

        // Merge live stats if the operation is running
        var liveStats = _manager.GetLiveStats(id);

        var totalResults = await _db.DeveloperResults.CountAsync(r => r.OperationId == id);
        var results = await _db.DeveloperResults
            .Where(r => r.OperationId == id)
            .OrderByDescending(r => r.FoundAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var vm = new OperationDetailViewModel
        {
            Id              = op.Id,
            Name            = op.Name,
            Status          = op.Status,
            Keywords        = JsonSerializer.Deserialize<List<string>>(op.KeywordsJson) ?? new(),
            Locations       = JsonSerializer.Deserialize<List<string>>(op.LocationsJson) ?? new(),
            MinFollowers    = op.MinFollowers,
            CreatedAt       = op.CreatedAt,
            StartedAt       = op.StartedAt,
            CompletedAt     = op.CompletedAt,
            ProfilesScanned = liveStats?.ProfilesScanned ?? op.ProfilesScanned,
            EmailsFound     = liveStats?.EmailsFound ?? op.EmailsFound,
            Failures        = liveStats?.Failures ?? op.Failures,
            CurrentUser     = liveStats?.CurrentUser ?? op.CurrentUser,
            CurrentQuery    = liveStats?.CurrentQuery ?? op.CurrentQuery,
            RateLimitRemaining = liveStats?.RateLimitRemaining ?? -1,
            Results         = results,
            TotalResults    = totalResults,
            Page            = page,
            PageSize        = pageSize
        };

        return View(vm);
    }

    // POST /operations/{id}/start
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(Guid id)
    {
        await _manager.StartAsync(id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    // POST /operations/start-all
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> StartAll()
    {
        var ops = await _manager.GetAllAsync();
        var toStart = ops
            .Where(o => o.Status != OperationStatus.Running)
            .Select(o => o.Id)
            .ToList();

        foreach (var opId in toStart)
            await _manager.StartAsync(opId);

        return RedirectToAction(nameof(Index));
    }

    // POST /operations/delete-all-keep-emails
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAllKeepEmails()
    {
        var allOps = await _manager.GetAllAsync();
        if (allOps.Count == 0)
            return RedirectToAction(nameof(Index));

        const string archiveName = "Archived Results";

        // Keep a single operation to preserve FK integrity for historical results.
        var archiveOperation = allOps
            .FirstOrDefault(o => string.Equals(o.Name, archiveName, StringComparison.OrdinalIgnoreCase));

        if (archiveOperation is null)
        {
            archiveOperation = await _manager.CreateAsync(
                archiveName,
                new List<string>(),
                new List<string>(),
                0,
                null);

            allOps.Add(archiveOperation);
        }

        var idsToDelete = allOps
            .Where(o => o.Id != archiveOperation.Id)
            .Select(o => o.Id)
            .ToList();

        if (idsToDelete.Count == 0)
            return RedirectToAction(nameof(Index));

        foreach (var opId in idsToDelete)
            await _manager.StopAsync(opId);

        await _db.DeveloperResults
            .Where(r => idsToDelete.Contains(r.OperationId))
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(r => r.OperationId, archiveOperation.Id));

        var operationsToDelete = await _db.Operations
            .Where(o => idsToDelete.Contains(o.Id))
            .ToListAsync();

        _db.Operations.RemoveRange(operationsToDelete);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // POST /operations/add-defaults
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDefaults()
    {
        await SeedData.SeedAsync(_db);
        return RedirectToAction(nameof(Index));
    }

    // POST /operations/{id}/stop
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Stop(Guid id)
    {
        await _manager.StopAsync(id);
        return RedirectToAction(nameof(Detail), new { id });
    }

    // POST /operations/{id}/delete
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _manager.DeleteAsync(id);
        return RedirectToAction(nameof(Index));
    }

    // GET /operations/{id}/export
    [HttpGet]
    public async Task<IActionResult> Export(Guid id)
    {
        var op = await _manager.GetAsync(id);
        if (op is null) return NotFound();

        var ms = new MemoryStream();
        await using (var writer = new StreamWriter(ms, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteHeader<CsvExportRow>();
            await csv.NextRecordAsync();

            var results = _db.DeveloperResults
                .Where(r => r.OperationId == id)
                .OrderBy(r => r.Username)
                .AsAsyncEnumerable();

            await foreach (var r in results)
            {
                csv.WriteRecord(new CsvExportRow
                {
                    Username        = r.Username,
                    Name            = r.Name ?? string.Empty,
                    Email           = r.Email,
                    EmailConfidence = r.EmailConfidence ?? string.Empty,
                    Location        = r.Location ?? string.Empty,
                    Website         = r.Website ?? string.Empty,
                    Followers       = r.Followers,
                    Repos           = r.Repos,
                    ProfileUrl      = r.ProfileUrl ?? string.Empty,
                    FoundAtUtc      = r.FoundAtUtc.ToString("o")
                });
                await csv.NextRecordAsync();
            }
        }

        ms.Position = 0;
        var filename = $"emails_{op.Name.Replace(' ', '_')}_{DateTime.UtcNow:yyyyMMddHHmm}.csv";
        return File(ms, "text/csv", filename);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static OperationCardDto ToCard(ScrapingOperation o) => new()
    {
        Id              = o.Id,
        Name            = o.Name,
        Status          = o.Status,
        EmailsFound     = o.EmailsFound,
        ProfilesScanned = o.ProfilesScanned,
        Failures        = o.Failures,
        CreatedAt       = o.CreatedAt,
        Keywords        = JsonSerializer.Deserialize<List<string>>(o.KeywordsJson) ?? new(),
        Locations       = JsonSerializer.Deserialize<List<string>>(o.LocationsJson) ?? new(),
        MinFollowers    = o.MinFollowers,
        CurrentUser     = o.CurrentUser,
        CurrentQuery    = o.CurrentQuery,
        StartedAt       = o.StartedAt,
        CompletedAt     = o.CompletedAt
    };

    // CSV export row type
    private sealed class CsvExportRow
    {
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmailConfidence { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Website { get; set; } = string.Empty;
        public int Followers { get; set; }
        public int Repos { get; set; }
        public string ProfileUrl { get; set; } = string.Empty;
        public string FoundAtUtc { get; set; } = string.Empty;
    }
}
