using System.Text.Json;
using GitHubScraper.Data;
using GitHubScraper.Services;
using GitHubScraper.ViewModels.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GitHubScraper.Controllers;

[Route("api/operations")]
[ApiController]
public sealed class ApiController : ControllerBase
{
    private readonly OperationManager _manager;
    private readonly AppDbContext _db;

    public ApiController(OperationManager manager, AppDbContext db)
    {
        _manager = manager;
        _db = db;
    }

    // GET /api/operations/{id}/stats  — polled by JS every 2 s when operation is Running
    [HttpGet("{id:guid}/stats")]
    public async Task<ActionResult<OperationStatsDto>> GetStats(Guid id)
    {
        var op = await _manager.GetAsync(id);
        if (op is null) return NotFound();

        var live = _manager.GetLiveStats(id);

        return new OperationStatsDto
        {
            Id                  = op.Id,
            Status              = op.Status.ToString(),
            ProfilesScanned     = live?.ProfilesScanned ?? op.ProfilesScanned,
            EmailsFound         = live?.EmailsFound ?? op.EmailsFound,
            Failures            = live?.Failures ?? op.Failures,
            CurrentUser         = live?.CurrentUser ?? op.CurrentUser,
            CurrentQuery        = live?.CurrentQuery ?? op.CurrentQuery,
            RateLimitRemaining  = live?.RateLimitRemaining ?? -1,
            RateLimitResetEpoch = live is not null
                ? live.RateLimitReset.ToUnixTimeSeconds()
                : 0,
            RequestsPerMinute   = live?.RequestsPerMinute ?? 0
        };
    }

    // GET /api/operations/{id}/results?page=1&pageSize=50&q=
    [HttpGet("{id:guid}/results")]
    public async Task<ActionResult<PagedResultsDto>> GetResults(
        Guid id, int page = 1, int pageSize = 50, string? q = null)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 200) pageSize = 50;

        var query = _db.DeveloperResults.Where(r => r.OperationId == id);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r =>
                r.Email.Contains(q) ||
                r.Username.Contains(q) ||
                (r.Name != null && r.Name.Contains(q)));

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.FoundAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ResultRowDto
            {
                Username        = r.Username,
                Name            = r.Name,
                Email           = r.Email,
                Location        = r.Location,
                EmailConfidence = r.EmailConfidence,
                Followers       = r.Followers,
                Repos           = r.Repos,
                ProfileUrl      = r.ProfileUrl,
                Website         = r.Website,
                FoundAtUtc      = r.FoundAtUtc
            })
            .ToListAsync();

        return new PagedResultsDto
        {
            Items      = items,
            TotalCount = total,
            Page       = page,
            PageSize   = pageSize,
            TotalPages = (int)Math.Ceiling((double)total / pageSize)
        };
    }

    // GET /api/operations/summary — lightweight list for Index page card refresh
    [HttpGet("summary")]
    public async Task<ActionResult<List<object>>> GetSummary()
    {
        var ops = await _manager.GetAllAsync();
        var result = ops.Select(op =>
        {
            var live = _manager.GetLiveStats(op.Id);
            return new
            {
                id              = op.Id,
                status          = op.Status.ToString(),
                profilesScanned = live?.ProfilesScanned ?? op.ProfilesScanned,
                emailsFound     = live?.EmailsFound ?? op.EmailsFound,
                failures        = live?.Failures ?? op.Failures,
                currentUser     = live?.CurrentUser ?? op.CurrentUser
            };
        }).ToList<object>();

        return result;
    }
}
