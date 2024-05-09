using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Cardano.Metadata.Models;
using Cardano.Metadata.Data;

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
    public async Task<IActionResult> GetSubjects(List<string> subjects)
    {
        using TokenMetadataDbContext db = await _dbFactory.CreateDbContextAsync();
        subjects = subjects.Select(s => s.ToLower()).ToList();
        List<TokenMetadata> metadataEntries = await db.TokenMetadata.Where(tmd => subjects.Contains(tmd.Subject.ToLower())).ToListAsync();
        return Ok(metadataEntries);
    }
}
