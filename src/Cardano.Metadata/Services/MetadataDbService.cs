using Cardano.Metadata.Models.Entity;
using Cardano.Metadata.Models.Response;
using Cardano.Metadata.Data;
using Microsoft.EntityFrameworkCore;
using Cardano.Metadata.Models.Github;

namespace Cardano.Metadata.Services;

public class MetadataDbService
(
    ILogger<MetadataDbService> logger,
    IDbContextFactory<MetadataDbContext> _dbContextFactory)
{
    public async Task<TokenMetadata?> AddTokenAsync(RegistryItem registryItem, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (string.IsNullOrEmpty(registryItem.Subject) ||
            registryItem.Name == null || string.IsNullOrEmpty(registryItem.Name.Value) ||
            registryItem.Ticker == null || string.IsNullOrEmpty(registryItem.Ticker.Value) ||
            registryItem.Decimals == null || registryItem.Decimals.Value < 0)
        {
            logger.LogWarning("Invalid token data. Name, Ticker, Subject or Decimals cannot be null or empty.");
            return null;
        }

        TokenMetadata token = new(
            registryItem.Subject,
            registryItem.Name.Value,
            registryItem.Ticker.Value,
            registryItem.Subject[..56],
            registryItem.Decimals.Value,
            registryItem.Policy ?? null,
            registryItem.Url?.Value ?? null,
            registryItem.Logo?.Value ?? null,
            registryItem.Description?.Value ?? null
        );

        await dbContext.TokenMetadata.AddAsync(token, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return token;
    }

    public async Task<SyncState?> GetSyncStateAsync(CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncState
            .OrderByDescending((SyncState ss) => ss.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertSyncStateAsync(GitCommit latestCommit, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        SyncState? syncState = await dbContext.SyncState.FirstOrDefaultAsync(cancellationToken);

        string newSha = latestCommit.Sha ?? string.Empty;
        DateTimeOffset newDate = latestCommit.Commit?.Author?.Date ?? DateTimeOffset.UtcNow;

        if (syncState is null)
        {
            syncState = new SyncState(newSha, newDate);
            await dbContext.SyncState.AddAsync(syncState, cancellationToken);
            logger.LogInformation("Sync state created.");
        }
        else
        {
            SyncState updatedSyncState = syncState with { Hash = newSha, Date = newDate };

            dbContext.Entry(syncState).CurrentValues.SetValues(updatedSyncState);
            logger.LogInformation("Sync state updated.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> SubjectExistsAsync(string subject, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TokenMetadata
            .AnyAsync(t => t.Subject == subject, cancellationToken);
    }
}
