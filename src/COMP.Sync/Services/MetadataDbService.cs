using COMP.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;
using COMP.Data.Models.Github;
using COMP.Data.Data;

namespace COMP.Sync.Services;

public class MetadataDbService
(
    ILogger<MetadataDbService> logger,
    IDbContextFactory<MetadataDbContext> _dbContextFactory)
{
    public async Task<TokenMetadataRegistry?> AddTokenAsync(TokenMetadataRegistry token, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (string.IsNullOrEmpty(token.Subject) ||
            string.IsNullOrEmpty(token.Name) ||
            string.IsNullOrEmpty(token.Description) ||
            token.Decimals < 0)
        {
            logger.LogWarning("Invalid token data. Subject, Name, Description required; Decimals must be non-negative if present.");
            return null;
        }

        await dbContext.TokenMetadataRegistry.AddAsync(token, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<RegistrySyncState?> GetRegistrySyncStateAsync(CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.RegistrySyncState
            .OrderByDescending(ss => ss.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertRegistrySyncStateAsync(GitCommit latestCommit, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        string newSha = latestCommit.Sha ?? string.Empty;
        DateTimeOffset newDate = latestCommit.Commit?.Author?.Date ?? DateTimeOffset.UtcNow;

        // Delete existing sync state if it exists
        RegistrySyncState? existingRegistrySyncState = await dbContext.RegistrySyncState
            .FirstOrDefaultAsync(cancellationToken);

        if (existingRegistrySyncState is not null)
        {
            string oldHash = existingRegistrySyncState.Hash;
            dbContext.RegistrySyncState.Remove(existingRegistrySyncState);
            logger.LogInformation("Removed old sync state with hash: {OldHash}", oldHash);
        }

        // Insert new sync state
        RegistrySyncState syncState = new(
            Hash: newSha,
            Date: newDate
        );
        await dbContext.RegistrySyncState.AddAsync(syncState, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sync state created with hash: {Hash}", newSha);
    }

    public async Task<HashSet<string>> GetExistingSubjectsAsync(IEnumerable<string> subjects, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        List<string> subjectList = subjects.ToList();
        List<string> existing = await dbContext.TokenMetadataRegistry
            .Where(t => subjectList.Contains(t.Subject))
            .Select(t => t.Subject)
            .ToListAsync(cancellationToken);
        return [.. existing];
    }

    public async Task<TokenMetadataRegistry?> UpdateTokenAsync(TokenMetadataRegistry updated, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(updated.Subject) ||
            string.IsNullOrEmpty(updated.Name) ||
            string.IsNullOrEmpty(updated.Description) ||
            updated.Decimals < 0)
        {
            logger.LogWarning("Invalid token data. Subject, Name, Description required; Decimals must be non-negative if present.");
            return null;
        }

        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.TokenMetadataRegistry.Update(updated);
        await dbContext.SaveChangesAsync(cancellationToken);
        return updated;
    }
}
