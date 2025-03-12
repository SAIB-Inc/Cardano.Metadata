using System.Text.Json;
using Metadata.Data;
using Metadata.Interface.GIthub;
using Metadata.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Metadata.Workers;

public class GithubWorker : BackgroundService
{
    private readonly ILogger<GithubWorker> _logger;
    private readonly IConfiguration _config;
    private readonly IDbContextFactory<MetadataDbContext> _dbContextFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly string _registryOwner;
    private readonly string _registryRepo;
    private readonly string _rawBaseUrl;

    public GithubWorker(
        ILogger<GithubWorker> logger,
        IConfiguration config,
        IDbContextFactory<MetadataDbContext> dbContextFactory,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _config = config;
        _dbContextFactory = dbContextFactory;
        _scopeFactory = scopeFactory;
        _registryOwner = config["RegistryOwner"] ?? throw new InvalidOperationException("RegistryOwner is not configured.");
        _registryRepo = config["RegistryRepo"] ?? throw new InvalidOperationException("RegistryRepo is not configured.");
        _rawBaseUrl = config["RawBaseUrl"] ?? throw new InvalidOperationException("RawBaseUrl is not configured.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Syncing Mappings");

            using var scope = _scopeFactory.CreateScope();
            var gitHubService = scope.ServiceProvider.GetRequiredService<IGithub>();

            using var dbContext = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
            var syncState = await dbContext.SyncState
                .OrderByDescending(ss => ss.Date)
                .FirstOrDefaultAsync(cancellationToken: stoppingToken);

            if (syncState is null)
            {
                await ProcessFullSyncAsync(gitHubService, dbContext, stoppingToken);
            }
            else
            {
                await ProcessIncrementalSyncAsync(gitHubService, dbContext, syncState, stoppingToken);
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessFullSyncAsync(IGithub gitHubService, MetadataDbContext dbContext, CancellationToken cancellationToken)
    {
        _logger.LogWarning("No Sync State Information, syncing all mappings...");

        var commits = await gitHubService.GetCommitsAsync(_registryOwner, _registryRepo, cancellationToken);
        var latestCommit = commits.FirstOrDefault();
        if (latestCommit is null)
        {
            _logger.LogError("Repo: {repo} Owner: {owner} has no commits!", _registryRepo, _registryOwner);
            return;
        }

        if (string.IsNullOrEmpty(latestCommit.Sha))
        {
            _logger.LogError("Latest commit SHA is null or empty. Cannot proceed with full sync.");
            return;
        }

        var treeResponse = await gitHubService.GetGitTreeAsync(_registryOwner, _registryRepo, latestCommit.Sha, cancellationToken);
        if (treeResponse?.Tree is not null)
        {
            foreach (var item in treeResponse.Tree)
            {
                if (item.Path is not null && item.Path.StartsWith("mappings/") && item.Path.EndsWith(".json"))
                {
                    var subject = item.Path.Replace("mappings/", string.Empty)
                                           .Replace(".json", string.Empty);
                    var rawUrl = $"{_rawBaseUrl}/{_registryOwner}/{_registryRepo}/{latestCommit.Sha}/{item.Path}";
                    await ProcessMappingFileAsync(gitHubService, rawUrl, subject, latestCommit.Sha, dbContext, cancellationToken);
                }
            }

            await UpdateSyncStateAsync(dbContext, latestCommit.Sha, latestCommit.Commit?.Author?.Date, cancellationToken);

            var totalCount = await dbContext.TokenMetadata.CountAsync(cancellationToken);
            _logger.LogInformation("Full sync complete. Total tokens in database: {count}", totalCount);
        }
        else
        {
            _logger.LogError("Repo: {repo} Owner: {owner} has no mappings!", _registryRepo, _registryOwner);
        }
    }

    private async Task ProcessIncrementalSyncAsync(IGithub gitHubService, MetadataDbContext dbContext, SyncState syncState, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Repo: {repo} Owner: {owner} checking for changes...", _registryRepo, _registryOwner);

        var commits = await gitHubService.GetCommitsSinceAsync(_registryOwner, _registryRepo, syncState.Date, cancellationToken);
        foreach (var commit in commits)
        {
            if (string.IsNullOrEmpty(commit.Sha))
            {
                _logger.LogWarning("Commit SHA is null or empty for commit {commitUrl}. Skipping this commit.", commit.Url);
                continue;
            }

            var resolvedCommit = await gitHubService.GetCommitDetailsAsync(commit.Url, cancellationToken);
            if (resolvedCommit?.Files is not null)
            {
                foreach (var file in resolvedCommit.Files)
                {
                    if (string.IsNullOrEmpty(file.Filename))
                        continue;

                    var subject = file.Filename.Replace("mappings/", string.Empty)
                                               .Replace(".json", string.Empty);
                    if (string.IsNullOrEmpty(resolvedCommit.Sha))
                    {
                        _logger.LogWarning("Resolved commit SHA is null or empty for file {fileName}. Skipping.", file.Filename);
                        continue;
                    }
                    var rawUrl = $"{_rawBaseUrl}/{_registryOwner}/{_registryRepo}/{resolvedCommit.Sha}/{file.Filename}";
                    try
                    {
                        await ProcessMappingFileAsync(gitHubService, rawUrl, subject, resolvedCommit.Sha, dbContext, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file for subject: {subject}. Deleting metadata if exists...", subject);
                        var existingMetadata = await dbContext.TokenMetadata
                            .FirstOrDefaultAsync(tm => tm.Subject.ToLower() == subject.ToLower(), cancellationToken: cancellationToken);
                        if (existingMetadata is not null)
                        {
                            dbContext.TokenMetadata.Remove(existingMetadata);
                        }
                    }
                }
            }
            await UpdateSyncStateAsync(dbContext, commit.Sha, commit.Commit?.Author?.Date, cancellationToken);

            var totalCount = await dbContext.TokenMetadata.CountAsync(cancellationToken);
            _logger.LogInformation("Incremental sync complete for commit {sha}. Total tokens in database: {count}", commit.Sha, totalCount);
        }
    }

    private async Task UpdateSyncStateAsync(MetadataDbContext dbContext, string? sha, DateTime? commitDate, CancellationToken cancellationToken)
    {
        await dbContext.SyncState.AddAsync(new SyncState
        {
            Sha = sha ?? string.Empty,
            Date = commitDate ?? DateTime.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessMappingFileAsync(IGithub gitHubService, string rawUrl, string subject, string commitSha, MetadataDbContext dbContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(commitSha))
        {
            _logger.LogWarning("Commit SHA is null or empty for file with subject {subject}. Skipping processing.", subject);
            return;
        }

        _logger.LogInformation("Processing mapping file for subject: {subject}", subject);

        var mappingJson = await gitHubService.GetMappingJsonAsync(rawUrl, cancellationToken);
        var mappingBytes = await gitHubService.GetMappingBytesAsync(rawUrl, cancellationToken);

        var token = CreateTokenMetadata(mappingJson, mappingBytes, subject);
        var existingToken = await dbContext.TokenMetadata
            .FirstOrDefaultAsync(tm => tm.Subject.ToLower() == subject.ToLower(), cancellationToken: cancellationToken);

        if (existingToken is not null)
        {
            existingToken.Name = token.Name;
            existingToken.Description = token.Description;
            existingToken.Policy = token.Policy;
            existingToken.Ticker = token.Ticker;
            existingToken.Url = token.Url;
            existingToken.Logo = token.Logo;
            existingToken.Decimals = token.Decimals;
            existingToken.Data = token.Data;
            _logger.LogDebug("Updated token: {subject}", subject);
        }
        else
        {
            await dbContext.TokenMetadata.AddAsync(token, cancellationToken);
            await dbContext.SaveChangesAsync();
            _logger.LogDebug("Inserting new token: {subject}, Name: {name}", subject, token.Name);
        }
    }

    private TokenMetadata CreateTokenMetadata(JsonElement mappingJson, byte[] mappingBytes, string subject)
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
            Decimals = decimals,
            Data = mappingBytes
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
