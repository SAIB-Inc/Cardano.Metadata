using System.Text.Json;
using Cardano.Metadata.Data;
using Cardano.Metadata.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Cardano.Metadata.Workers.Handlers;

public class DatabaseHandler(ILogger<Github> logger, IDbContextFactory<MetadataDbContext> dbContextFactory)
{
    private readonly ILogger<Github> _logger = logger;
    private readonly IDbContextFactory<MetadataDbContext> _dbContextFactory = dbContextFactory;

    public async Task UpdateSyncStateAsync(string sha, DateTime? commitDate, CancellationToken cancellationToken)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.SyncState.AddAsync(new SyncState
        {
            Sha = sha ?? string.Empty,
            Date = commitDate ?? DateTime.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TokenMetadata> GetOrCreateTokenAsync(JsonElement mappingJson, byte[] mappingBytes, string subject, CancellationToken cancellationToken)
    {

        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingToken = await dbContext.TokenMetadata
            .FirstOrDefaultAsync(tm => tm.Subject != null && tm.Subject.ToLower() == subject.ToLower(), cancellationToken);


        var token = CreateTokenMetadata(mappingJson, mappingBytes, subject);
        if (existingToken != null)
        {
            existingToken.Name = token.Name;
            existingToken.Description = token.Description;
            existingToken.Policy = token.Policy;
            existingToken.Ticker = token.Ticker;
            existingToken.Url = token.Url;
            existingToken.Logo = token.Logo;
            existingToken.Decimals = token.Decimals;
            _logger.LogDebug("Updated token: {subject}", subject);
        }
        else
        {
            await dbContext.TokenMetadata.AddAsync(token, cancellationToken);
            await dbContext.SaveChangesAsync();
            _logger.LogDebug("Inserted new token: {subject}, Name: {name}", subject, token.Name);
        }

        return token;
    }

    public async Task<SyncState?> GetLatestSyncStateAsync(CancellationToken cancellationToken)
    {
        using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncState
            .OrderByDescending(ss => ss.Date)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    private static TokenMetadata CreateTokenMetadata(JsonElement mappingJson, byte[] mappingBytes, string subject)
    {
        string name = GetNestedValue(mappingJson, "name");
        string description = GetNestedValue(mappingJson, "description");
        string ticker = GetNestedValue(mappingJson, "ticker");
        string url = GetNestedValue(mappingJson, "url");
        string logo = GetNestedValue(mappingJson, "logo");
        int decimals = GetNestedInt(mappingJson, "decimals");
        var policy = subject.Length >= 56 ? subject.Substring(0, 56) : subject;

        return new TokenMetadata
        {
            Subject = subject,
            Name = name,
            Description = description,
            Policy = policy,
            Ticker = ticker,
            Url = url,
            Logo = logo,
            Decimals = decimals
        };
    }

    private static string GetNestedValue(JsonElement json, string propertyName)
    {
        if (json.TryGetProperty(propertyName, out var propertyElement) &&
            propertyElement.TryGetProperty("value", out var valueElement))
        {
            return valueElement.GetString() ?? string.Empty;
        }
        return string.Empty;
    }

    private static int GetNestedInt(JsonElement json, string propertyName)
    {
        if (json.TryGetProperty(propertyName, out var propertyElement) &&
            propertyElement.TryGetProperty("value", out var valueElement) &&
            valueElement.TryGetInt32(out int result))
        {
            return result;
        }
        return 0;
    }
}