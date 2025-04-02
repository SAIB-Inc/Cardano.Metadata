using System.Text.Json;
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
    public RegistryItem? DeserializeJson(JsonElement mappingJson)
    {
        var registryItem = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(mappingJson.GetRawText())
                           ?? throw new InvalidOperationException("Failed to deserialize mappingJson into a Dictionary.");

        var subject = registryItem.TryGetValue("subject", out JsonElement subjectElement)
            ? subjectElement.GetString()
            : null;

        var name = registryItem.TryGetValue("name", out JsonElement nameElement)
            ? nameElement.GetProperty("value").GetString()
            : null;

        var ticker = registryItem.TryGetValue("ticker", out JsonElement tickerElement)
            ? tickerElement.GetProperty("value").GetString()
            : null;

        var description = registryItem.TryGetValue("description", out JsonElement descriptionElement)
            ? descriptionElement.GetProperty("value").GetString()
            : null;

        var policy = registryItem.TryGetValue("policy", out JsonElement policyElement)
            ? policyElement.GetString()
            : null;

        var url = registryItem.TryGetValue("url", out JsonElement urlElement)
            ? urlElement.GetProperty("value").GetString()
            : null;

        var logo = registryItem.TryGetValue("logo", out JsonElement logoElement)
            ? logoElement.GetProperty("value").GetString()
            : null;

        var decimals = registryItem.TryGetValue("decimals", out JsonElement decimalsElement)
            ? decimalsElement.GetProperty("value").GetInt32()
            : 0;


        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ticker))
        {
            logger.LogWarning("Invalid token data. Subject, Name, and Ticker cannot be null or empty.");
            return null;
        }

        var result = new RegistryItem
        {
            Subject = subject,
            Policy = policy,
            Name = new ValueResponse<string> { Value = name },
            Ticker = new ValueResponse<string> { Value = ticker },
            Description = new ValueResponse<string> { Value = description },
            Url = new ValueResponse<string> { Value = url },
            Logo = new ValueResponse<string> { Value = logo },
            Decimals = new ValueResponse<int> { Value = decimals }
        };

        return result;
    }

    public async Task<TokenMetadata?> AddTokenAsync(JsonElement mappingJson, CancellationToken cancellationToken)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var registryItem = DeserializeJson(mappingJson);

        if (registryItem == null ||
            string.IsNullOrEmpty(registryItem.Subject) ||
            registryItem.Name == null || string.IsNullOrEmpty(registryItem.Name.Value) ||
            registryItem.Ticker == null || string.IsNullOrEmpty(registryItem.Ticker.Value) ||
            registryItem.Decimals == null || registryItem.Decimals.Value < 0)
        {
            logger.LogWarning("Invalid token data. Name, Ticker, Subject or Decimals cannot be null or empty.");
            return null;
        }

        bool exists = await dbContext.TokenMetadata
        .AnyAsync(t => t.Subject == registryItem.Subject, cancellationToken);

        if (exists)
        {
            logger.LogWarning("Token with subject {Subject} already exists.", registryItem.Subject);
            return null;
        }

        var token = new TokenMetadata(
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
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncState
            .OrderByDescending(ss => ss.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddOrUpdateSyncStateAsync(GitCommit latestCommit, CancellationToken cancellationToken)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var syncState = await dbContext.SyncState.FirstOrDefaultAsync(cancellationToken);

        var newSha = latestCommit.Sha ?? string.Empty;
        var newDate = latestCommit.Commit?.Author?.Date ?? DateTimeOffset.UtcNow;

        if (syncState is null)
        {
            syncState = new SyncState(newSha, newDate);
            await dbContext.SyncState.AddAsync(syncState, cancellationToken);
            logger.LogInformation("Sync state created.");
        }
        else
        {
            var updatedSyncState = syncState with { Hash = newSha, Date = newDate };

            dbContext.Entry(syncState).CurrentValues.SetValues(updatedSyncState);
            logger.LogInformation("Sync state updated.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
