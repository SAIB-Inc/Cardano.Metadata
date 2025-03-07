using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Metadata.Data;
using Metadata.Models.Entity;
using Metadata.Models.Github;
using Microsoft.EntityFrameworkCore;

namespace Metadata.Workers
{
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
                Console.WriteLine("Starting sync cycle at " + DateTime.UtcNow);
                _logger.LogInformation("Syncing Mappings");

                using var dbContext = await _dbContextFactory.CreateDbContextAsync(stoppingToken);
                var syncState = await dbContext.SyncState
                    .OrderByDescending(ss => ss.Date)
                    .FirstOrDefaultAsync(cancellationToken: stoppingToken);

                var registryOwner = _config["RegistryOwner"] ?? throw new InvalidOperationException("RegistryOwner is not configured.");
                var registryRepo = _config["RegistryRepo"] ?? throw new InvalidOperationException("RegistryRepo is not configured.");
                var githubPat = _config["GithubPAT"] ?? throw new InvalidOperationException("GithubPAT is not configured.");

                var httpClient = _httpClientFactory.CreateClient("Github");
                ConfigureHttpClient(httpClient, githubPat);

                if (syncState is null)
                {
                    Console.WriteLine("No sync state found, performing full sync...");
                    await ProcessFullSyncAsync(httpClient, dbContext, stoppingToken, registryOwner, registryRepo);
                }
                else
                {
                    Console.WriteLine("Sync state found, performing incremental sync...");
                    await ProcessIncrementalSyncAsync(httpClient, dbContext, stoppingToken, registryOwner, registryRepo, syncState);
                }

                Console.WriteLine("Sync cycle complete. Waiting for next cycle...");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private void ConfigureHttpClient(HttpClient httpClient, string githubPat)
        {
            httpClient.DefaultRequestHeaders.UserAgent.Clear();
            var productVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version";
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CardanoTokenMetadataService", productVersion));
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/SAIB-Inc/Cardano.Metadata)"));
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubPat);
        }

        private async Task ProcessFullSyncAsync(HttpClient httpClient, TokenMetadataDbContext dbContext, CancellationToken cancellationToken, string registryOwner, string registryRepo)
        {
            Console.WriteLine("Starting full sync...");
            _logger.LogWarning("No Sync State Information, syncing all mappings...");

            var commitsUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/commits";
            var latestCommits = await httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);

            if (latestCommits is not null && latestCommits.Any())
            {
                var latestCommit = latestCommits.First();
                var treeUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/git/trees/{latestCommit.Sha}?recursive=true";
                var treeResponse = await httpClient.GetFromJsonAsync<GitTreeResponse>(treeUrl, cancellationToken);

                if (treeResponse?.Tree is not null)
                {
                    foreach (var item in treeResponse.Tree)
                    {
                        if (item.Path is not null &&
                            item.Path.StartsWith("mappings/") &&
                            item.Path.EndsWith(".json"))
                        {
                            var subject = item.Path.Replace("mappings/", string.Empty)
                                                   .Replace(".json", string.Empty);
                            var rawUrl = $"https://raw.githubusercontent.com/{registryOwner}/{registryRepo}/{latestCommit.Sha}/{item.Path}";

                            Console.WriteLine($"Processing mapping file: {rawUrl}");
                            var mappingJson = await httpClient.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: cancellationToken);
                            var mappingBytes = await httpClient.GetByteArrayAsync(rawUrl, cancellationToken);

                            var name = GetNestedValue(mappingJson, "name");
                            var description = GetNestedValue(mappingJson, "description");
                            var ticker = GetNestedValue(mappingJson, "ticker");
                            var url = GetNestedValue(mappingJson, "url");
                            var logo = GetNestedValue(mappingJson, "logo");
                            var decimals = GetNestedInt(mappingJson, "decimals");
                            var policy = subject.Length >= 56 ? subject.Substring(0, 56) : subject;

                            var token = new TokenMetadata
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

                            Console.WriteLine($"Inserting token: {subject}, Name: {name}");
                            _logger.LogDebug("Inserting token with Subject: {subject}, Name: {name}", subject, name);

                            await dbContext.TokenMetadata.AddAsync(token, cancellationToken);
                            await dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }

                    await dbContext.SyncState.AddAsync(new SyncState
                    {
                        Sha = latestCommit.Sha ?? string.Empty,
                        Date = latestCommit.Commit?.Author?.Date ?? DateTime.UtcNow
                    }, cancellationToken);

                    await dbContext.SaveChangesAsync(cancellationToken);

                    var totalCount = await dbContext.TokenMetadata.CountAsync(cancellationToken);
                    Console.WriteLine($"Full sync complete. Total tokens in database: {totalCount}");
                    _logger.LogInformation("Full sync complete. Total tokens in database: {count}", totalCount);
                }
                else
                {
                    Console.WriteLine("No mapping files found in the repository tree.");
                    _logger.LogError("Repo: {repo} Owner: {owner} has no mappings!", registryRepo, registryOwner);
                }
            }
            else
            {
                Console.WriteLine("No commits found in repository.");
                _logger.LogError("Repo: {repo} Owner: {owner} has no commits!", registryRepo, registryOwner);
            }
        }

        private async Task ProcessIncrementalSyncAsync(HttpClient httpClient, TokenMetadataDbContext dbContext, CancellationToken cancellationToken, string registryOwner, string registryRepo, SyncState syncState)
        {
            Console.WriteLine("Starting incremental sync...");
            _logger.LogInformation("Repo: {repo} Owner: {owner} checking for changes...", registryRepo, registryOwner);

            var latestCommitsSince = new List<GitCommit>();
            int page = 1;
            var since = syncState.Date.AddSeconds(1).ToString("yyyy-MM-dd'T'HH:mm:ssZ");

            while (true)
            {
                var commitsPageUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/commits?since={since}&page={page}";
                var commitPage = await httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsPageUrl, cancellationToken);
                if (commitPage is null || !commitPage.Any())
                    break;

                latestCommitsSince.AddRange(commitPage);
                page++;
            }

            foreach (var commit in latestCommitsSince)
            {
                Console.WriteLine($"Processing incremental commit: {commit.Sha}");
                var resolvedCommit = await httpClient.GetFromJsonAsync<GitCommit>(commit.Url, cancellationToken: cancellationToken);
                if (resolvedCommit?.Files is not null)
                {
                    foreach (var file in resolvedCommit.Files)
                    {
                        if (string.IsNullOrEmpty(file.Filename))
                            continue;

                        var subject = file.Filename.Replace("mappings/", string.Empty)
                                                   .Replace(".json", string.Empty);
                        var rawUrl = $"https://raw.githubusercontent.com/{registryOwner}/{registryRepo}/{resolvedCommit.Sha}/{file.Filename}";

                        try
                        {
                            Console.WriteLine($"Processing file: {rawUrl}");
                            var mappingJson = await httpClient.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: cancellationToken);
                            var mappingBytes = await httpClient.GetByteArrayAsync(rawUrl, cancellationToken);

                            var name = GetNestedValue(mappingJson, "name");
                            var description = GetNestedValue(mappingJson, "description");
                            var ticker = GetNestedValue(mappingJson, "ticker");
                            var url = GetNestedValue(mappingJson, "url");
                            var logo = GetNestedValue(mappingJson, "logo");
                            var decimals = GetNestedInt(mappingJson, "decimals");
                            var policy = subject.Length >= 56 ? subject.Substring(0, 56) : subject;

                            var existingMetadata = await dbContext.TokenMetadata
                                .FirstOrDefaultAsync(tm => tm.Subject.ToLower() == subject.ToLower(), cancellationToken: cancellationToken);

                            if (existingMetadata is not null)
                            {
                                existingMetadata.Data = mappingBytes;
                                existingMetadata.Name = name;
                                existingMetadata.Description = description;
                                existingMetadata.Policy = policy;
                                existingMetadata.Ticker = ticker;
                                existingMetadata.Url = url;
                                existingMetadata.Logo = logo;
                                existingMetadata.Decimals = decimals;
                                Console.WriteLine($"Updated token: {subject}");
                                _logger.LogDebug("Updated token: {subject}", subject);
                            }
                            else
                            {
                                var newToken = new TokenMetadata
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
                                Console.WriteLine($"Inserting new token: {subject}, Name: {name}");
                                _logger.LogDebug("Inserting new token: {subject}, Name: {name}", subject, name);
                                await dbContext.TokenMetadata.AddAsync(newToken, cancellationToken);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing file {rawUrl}: {ex.Message}");
                            _logger.LogError(ex, "Error processing file: {file}. Deleting metadata if exists...", rawUrl);
                            var existingMetadata = await dbContext.TokenMetadata
                                .FirstOrDefaultAsync(tm => tm.Subject.ToLower() == subject.ToLower(), cancellationToken: cancellationToken);
                            if (existingMetadata is not null)
                            {
                                dbContext.TokenMetadata.Remove(existingMetadata);
                            }
                        }
                    }
                }

                await dbContext.SyncState.AddAsync(new SyncState
                {
                    Sha = commit.Sha ?? string.Empty,
                    Date = commit.Commit?.Author?.Date ?? DateTime.UtcNow
                }, cancellationToken);

                await dbContext.SaveChangesAsync(cancellationToken);

                var totalCount = await dbContext.TokenMetadata.CountAsync(cancellationToken);
                Console.WriteLine($"Incremental sync complete for commit {commit.Sha}. Total tokens in database: {totalCount}");
                _logger.LogInformation("Incremental sync complete for commit {sha}. Total tokens in database: {count}", commit.Sha, totalCount);
            }
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
}