using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cardano.Metadata.Models;
using Cardano.Metadata.Data;
using System.Text;

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

    [HttpPost]
    public async Task<IActionResult> GetSubjects(
        [FromQuery] int? limit,
        [FromQuery] string? searchKey,
        [FromBody] List<string>? subjects,
        [FromQuery] int offset = 0
    )
    {
        using TokenMetadataDbContext db = await _dbFactory.CreateDbContextAsync();
        IQueryable<TokenMetadata> query = db.TokenMetadata;

        // Filter by subjects
        if (subjects is not null)
        {
            subjects = subjects.Select(s => s.ToLower()).ToList();
            query = query.Where(tmd => subjects.Contains(tmd.Subject.ToLower()));
        }

        // Filter by search key
        if (!string.IsNullOrWhiteSpace(searchKey))
        {
            query = query.Where(tmd => tmd.Subject.Contains(searchKey.ToLower()));
        }

        // Get total count before pagination
        int total = await query.CountAsync();

        // Sort
        query = query.OrderBy(tmd => tmd.Subject.Substring(56));

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
