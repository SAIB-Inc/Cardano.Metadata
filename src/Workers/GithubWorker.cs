using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Cardano.Metadata.Data;
using Cardano.Metadata.Models;
using Microsoft.EntityFrameworkCore;

namespace Cardano.Metadata.Workers;

public class GithubWorker : BackgroundService
{
    private readonly ILogger<GithubWorker> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDbContextFactory<TokenMetadataDbContext> _dbContextFactory;

    public GithubWorker(
        ILogger<GithubWorker> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        IDbContextFactory<TokenMetadataDbContext> dbContextFactory)
    {
        _logger = logger;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _dbContextFactory = dbContextFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Syncing Mappings");

            using TokenMetadataDbContext dbContext = await _dbContextFactory.CreateDbContextAsync(stoppingToken);

            SyncState? syncState = await dbContext.SyncState.OrderByDescending(ss => ss.Date).FirstOrDefaultAsync(cancellationToken: stoppingToken);

            HttpClient hc = _httpClientFactory.CreateClient("Github");
            ProductInfoHeaderValue productValue = new("CardanoTokenMetadataService", Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version");
            ProductInfoHeaderValue commentValue = new("(+https://github.com/SAIB-Inc/Cardano.Metadata)");
            hc.DefaultRequestHeaders.UserAgent.Add(productValue);
            hc.DefaultRequestHeaders.UserAgent.Add(commentValue);
            hc.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config["GithubPAT"]);


            if (syncState is null)
            {
                _logger.LogWarning("No Sync State Information, syncing all mappings...");

                IEnumerable<GitCommit>? latestCommits = await hc
                    .GetFromJsonAsync<IEnumerable<GitCommit>>(
                        $"https://api.github.com/repos/{_config["RegistryOwner"]}/{_config["RegistryRepo"]}/commits",
                        stoppingToken
                    );

                if (latestCommits is not null && latestCommits.Any())
                {
                    GitCommit latestCommit = latestCommits.First();
                    GitTreeResponse? treeResponse = await hc
                        .GetFromJsonAsync<GitTreeResponse>(
                            $"https://api.github.com/repos/{_config["RegistryOwner"]}/{_config["RegistryRepo"]}/git/trees/{latestCommit.Sha}?recursive=true",
                            stoppingToken
                        );

                    if (treeResponse is not null && treeResponse.Tree is not null)
                    {
                        foreach (GitTreeItem item in treeResponse.Tree)
                        {
                            if (item.Path is not null && item.Path.StartsWith("mappings/") && item.Path.EndsWith(".json"))
                            {
                                string subject = item.Path
                                    .Replace("mappings/", string.Empty)
                                    .Replace(".json", string.Empty);

                                JsonElement mappingJson =
                                    await hc.GetFromJsonAsync<JsonElement>($"https://raw.githubusercontent.com/{_config["RegistryOwner"]}/{_config["RegistryRepo"]}/{latestCommit.Sha}/{item.Path}", cancellationToken: stoppingToken);

                                await dbContext.TokenMetadata.AddAsync(new()
                                {
                                    Subject = subject,
                                    Data = mappingJson
                                }, stoppingToken);
                            }
                        }

                        await dbContext.SyncState.AddAsync(new()
                        {
                            // @TODO handle null from API???
                            Sha = latestCommit.Sha ?? string.Empty,
                            Date = latestCommit.Commit?.Author?.Date ?? DateTime.UtcNow
                        }, stoppingToken);

                        await dbContext.SaveChangesAsync(stoppingToken);
                    }
                    else
                    {
                        _logger.LogError("Repo: {repo} Owner: {owner} has no mappings!", _config["RegistryOwner"], _config["RegistryRepo"]);
                        break;
                    }
                }
                else
                {
                    _logger.LogError("Repo: {repo} Owner: {owner} has no commits!", _config["RegistryOwner"], _config["RegistryRepo"]);
                    break;
                }
            }
            else
            {
                _logger.LogInformation("Repo: {repo} Owner: {owner} checking for changes...", _config["RegistryOwner"], _config["RegistryRepo"]);
                List<GitCommit> latestCommitsSince = [];

                int page = 1;
                while (true)
                {
                    IEnumerable<GitCommit>? commitPage = await hc.GetFromJsonAsync<IEnumerable<GitCommit>>($"https://api.github.com/repos/{_config["RegistryOwner"]}/{_config["RegistryRepo"]}/commits?since={syncState.Date.AddSeconds(1).ToString("yyyy-MM-dd'T'HH:mm:ssZ")}&page={page}"
, cancellationToken: stoppingToken);
                    if (commitPage is null || !commitPage.Any()) break;
                    latestCommitsSince.AddRange(commitPage);
                    page++;
                }

                foreach (GitCommit commit in latestCommitsSince)
                {
                    GitCommit? resolvedCommit = await hc.GetFromJsonAsync<GitCommit>(commit.Url, cancellationToken: stoppingToken);
                    if (resolvedCommit is not null && resolvedCommit.Files is not null)
                    {
                        foreach (GitCommitFile file in resolvedCommit.Files)
                        {
                            if (file.Filename is not null)
                            {
                                string subject = file.Filename
                                        .Replace("mappings/", string.Empty)
                                        .Replace(".json", string.Empty);
                                string rawUrl = $"https://raw.githubusercontent.com/{_config["RegistryOwner"]}/{_config["RegistryRepo"]}/{resolvedCommit.Sha}/{file.Filename}";

                                try
                                {

                                    JsonElement mappingJson =
                                        await hc.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: stoppingToken);
                                    TokenMetadata? existingMetadata = await dbContext.TokenMetadata.Where(tm => tm.Subject.ToLower() == subject.ToLower()).FirstOrDefaultAsync(cancellationToken: stoppingToken);

                                    if (existingMetadata is not null)
                                    {
                                        existingMetadata.Data = mappingJson;
                                    }
                                    else
                                    {
                                        await dbContext.TokenMetadata.AddAsync(new()
                                        {
                                            Subject = subject,
                                            Data = mappingJson
                                        }, stoppingToken);
                                    }
                                    _logger.LogInformation("Repo: {repo} Owner: {owner} Subject: {subject} added/updated...", _config["RegistryOwner"], _config["RegistryRepo"], subject);
                                }
                                catch
                                {
                                    _logger.LogInformation("Repo: {repo} Owner: {owner} File: {file} not found, deleting metadata...", _config["RegistryOwner"], _config["RegistryRepo"], rawUrl);
                                    TokenMetadata? existingMetadata = await dbContext.TokenMetadata.Where(tm => tm.Subject.ToLower() == subject.ToLower()).FirstOrDefaultAsync(cancellationToken: stoppingToken);
                                    if (existingMetadata is not null)
                                    {
                                        dbContext.TokenMetadata.Remove(existingMetadata);
                                    }
                                }
                                await dbContext.SaveChangesAsync(stoppingToken);
                            }
                        }
                    }

                    await dbContext.SyncState.AddAsync(new()
                    {
                        // @TODO handle null from API???
                        Sha = commit.Sha ?? string.Empty,
                        Date = commit.Commit?.Author?.Date ?? DateTime.UtcNow
                    }, stoppingToken);

                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }

            await Task.Delay(1000 * 60, stoppingToken);
        }
    }
}