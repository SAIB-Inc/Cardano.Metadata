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
            .OrderByDescending(ss => ss.Date)
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
    public async Task DeleteMissingMetadataAsync(string subject, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        TokenMetadata? existingMetadata = await dbContext.TokenMetadata
            .Where(tm => tm.Subject.Equals(subject, StringComparison.CurrentCultureIgnoreCase))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingMetadata is not null)
        {
            dbContext.TokenMetadata.Remove(existingMetadata);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }


    public async Task<TokenMetadata?> UpdateTokenAsync(RegistryItem registryItem, CancellationToken cancellationToken)
    {
        using MetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        TokenMetadata? existingMetadata = await dbContext.TokenMetadata
            .FirstOrDefaultAsync(t => t.Subject == registryItem.Subject, cancellationToken);

        if (existingMetadata is null)
        {
            logger.LogWarning("Token metadata not found for subject {Subject}", registryItem.Subject);
            return null;
        }

        if (registryItem.Name == null || string.IsNullOrEmpty(registryItem.Name.Value) ||
           registryItem.Ticker == null || string.IsNullOrEmpty(registryItem.Ticker.Value) ||
           registryItem.Decimals == null || registryItem.Decimals.Value < 0)
        {
            logger.LogWarning("Invalid token data. Name, Ticker, Subject or Decimals cannot be null or empty.");
            return null;
        }


        TokenMetadata updatedMetadata = existingMetadata with
        {
            Name = registryItem.Name.Value,
            Ticker = registryItem.Ticker.Value,
            Decimals = registryItem.Decimals.Value,
            Policy = registryItem.Policy,
            Url = registryItem.Url?.Value,
            Logo = registryItem.Logo?.Value,
            Description = registryItem.Description?.Value
        };

        dbContext.TokenMetadata.Update(updatedMetadata);
        await dbContext.SaveChangesAsync(cancellationToken);
        return updatedMetadata;
    }


}
