using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cardano.Metadata.Models;
using Cardano.Metadata.Data;
using Microsoft.AspNetCore.OutputCaching;

namespace Cardano.Metadata.Controllers;

[ApiController]
[Route("metadata")]
public class TokenMetadataController : ControllerBase
{

    private readonly ILogger<TokenMetadataController> _logger;
    private readonly IDbContextFactory<TokenMetadataDbContext> _dbFactory;

    public TokenMetadataController(ILogger<TokenMetadataController> logger, IDbContextFactory<TokenMetadataDbContext> dbFactory)
    {
        _logger = logger;
        _dbFactory = dbFactory;
    }

    [OutputCache]
    [HttpGet("{subject}")]
    public async Task<IActionResult> Get(string subject)
    {
        using TokenMetadataDbContext db = await _dbFactory.CreateDbContextAsync();
        TokenMetadata? metadataEntry = await db.TokenMetadata.Where(tmd => tmd.Subject.ToLower() == subject.ToLower()).FirstOrDefaultAsync();
        if (metadataEntry is not null)
        {
            return Ok(metadataEntry.Data);
        }
        else
        {
            return NotFound();
        }
    }

    [OutputCache(PolicyName = "CachePost")]
    [HttpPost]
    public async Task<IActionResult> GetSubjects(
        [FromBody] List<string>? subjects,
        [FromQuery] int? limit,
        [FromQuery] string? searchKey,
        [FromQuery] string? policyId,
        [FromQuery] int offset = 0,
        [FromQuery] bool includeEmptyName = false,
        [FromQuery] bool includeEmptyLogo = false,
        [FromQuery] bool includeEmptyTicker = false
    )
    {
        if (subjects is not null && subjects.Count == 1 && subjects.First() == string.Empty)
        {
            return Ok(new { total = 0, data = new List<TokenMetadata>() });
        }

        using TokenMetadataDbContext db = await _dbFactory.CreateDbContextAsync();

        IQueryable<TokenMetadata> query = db.TokenMetadata;

        // Filter by policy id
        if (!string.IsNullOrWhiteSpace(policyId))
            query = query.Where(tmd => tmd.Subject.Substring(0, 56).ToLower() == policyId);

        // Filter by empty name
        if (!includeEmptyName)
            query = query.Where(tmd => EF.Functions.JsonExists(tmd.Data, "name"));

        if (!includeEmptyLogo)
            query = query.Where(tmd => EF.Functions.JsonExists(tmd.Data, "logo"));

        if (!includeEmptyTicker)
            query = query.Where(tmd => EF.Functions.JsonExists(tmd.Data, "ticker"));

        // Filter by subjects
        if (subjects is not null && subjects.Count > 0)
        {
            var lowerSubjects = subjects.Select(s => s.ToLower()).ToList();
            query = query.Where(tmd => lowerSubjects.Contains(tmd.Subject.ToLower()));
        }

        // Filter by search key
        if (!string.IsNullOrWhiteSpace(searchKey))
            query = query.Where(tmd =>
                EF.Functions.Like(tmd.Data.GetProperty("name").GetProperty("value").GetString()!.ToLower(), $"%{searchKey.ToLower()}%") ||
                EF.Functions.Like(tmd.Data.GetProperty("ticker").GetProperty("value").GetString()!.ToLower(), $"%{searchKey.ToLower()}%")
            );

        // Get total count before pagination
        int total = await query.CountAsync();

        // Sort
        query = query.OrderBy(tmd => tmd.Data.GetProperty("ticker").GetProperty("value").GetString()!.ToLower());

        // Skip offset
        query = query.Skip(offset);

        // Take if limit is provided
        if (limit is not null)
        {
            query = query.Take(limit.Value);
        }

        // Execute query
        List<TokenMetadata> metadataEntries = await query.ToListAsync();

        return Ok(new
        {
            total,
            data = metadataEntries
        });
    }
}
