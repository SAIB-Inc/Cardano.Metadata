using System.Text.Json;
using Cardano.Metadata.Models.Entity;
using Cardano.Metadata.Models.Response;
using Cardano.Metadata.Data;
using Microsoft.EntityFrameworkCore;

namespace Cardano.Metadata.Services;

public class MetadataDbService(ILogger<MetadataDbService> logger, IDbContextFactory<MetadataDbContext> _dbContextFactory)
{
    private readonly ILogger<MetadataDbService> _logger = logger;

    public RegistryItem? CreateTokenMetadata(JsonElement mappingJson)
    {
        var registryItem = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(mappingJson.GetRawText())
                           ?? throw new InvalidOperationException("Failed to deserialize mappingJson into a Dictionary.");

        var subject = registryItem.ContainsKey("subject") ? registryItem["subject"].GetString() : null;
        var name = registryItem.ContainsKey("name") ? registryItem["name"].GetProperty("value").GetString() : null;
        var ticker = registryItem.ContainsKey("ticker") ? registryItem["ticker"].GetProperty("value").GetString() : null;
        var description = registryItem.ContainsKey("description") ? registryItem["description"].GetProperty("value").GetString() : null;
        var policy = registryItem.ContainsKey("policy") ? registryItem["policy"].GetString() : null;
        var url = registryItem.ContainsKey("url") ? registryItem["url"].GetProperty("value").GetString() : null;
        var logo = registryItem.ContainsKey("logo") ? registryItem["logo"].GetProperty("value").GetString() : null;
        var decimals = registryItem.ContainsKey("decimals") ? registryItem["decimals"].GetProperty("value").GetInt32() : 0;

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ticker))
        {
            _logger.LogWarning("Invalid token data. Subject, Name, and Ticker cannot be null or empty.");
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

    public async Task<MetaData?> CreateTokenAsync(JsonElement mappingJson, CancellationToken cancellationToken)
    {
        var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var registryItem = CreateTokenMetadata(mappingJson);

        if (registryItem == null ||
            string.IsNullOrEmpty(registryItem.Subject) ||
            registryItem.Name == null || string.IsNullOrEmpty(registryItem.Name.Value) ||
            registryItem.Ticker == null || string.IsNullOrEmpty(registryItem.Ticker.Value) ||
            registryItem.Decimals == null || registryItem.Decimals.Value <= 0)
        {
            _logger.LogWarning("Invalid token data. Name, Ticker, Subject or Decimals cannot be null or empty.");
            return null;
        }

        var token = new MetaData(
            registryItem.Subject,
            registryItem.Name.Value,
            registryItem.Ticker.Value,
            registryItem.Subject.Substring(0, 56),
            registryItem.Decimals.Value,
            registryItem.Policy ?? null,
            registryItem.Url?.Value ?? null,
            registryItem.Logo?.Value ?? null,
            registryItem.Description?.Value ?? null
        );

        await dbContext.MetaData.AddAsync(token, cancellationToken);
        return token;
    }
}
