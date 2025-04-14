using System.Text.Json;
using Cardano.Metadata.Models.Entity;
using Cardano.Metadata.Models.Github;
using Cardano.Metadata.Models.Response;
using Cardano.Metadata.Services;

namespace Cardano.Metadata.Workers;
public class GithubWorker
(
    ILogger<GithubWorker> logger,
    GithubService githubService,
    MetadataDbService metadataDbService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Syncing Mappings");

                SyncState? syncState = await metadataDbService.GetSyncStateAsync(stoppingToken);

                if (syncState is null)
                {
                    logger.LogWarning("No Sync State Information, syncing all mappings...");
                    GitCommit? latestCommit = await githubService.GetCommitsAsync(stoppingToken);

                    if (latestCommit == null || string.IsNullOrEmpty(latestCommit.Sha))
                    {
                        logger.LogError("Commit SHA is null or empty for the latest commit.");
                        break;
                    }
                    GitTreeResponse? treeResponse = await githubService.GetGitTreeAsync(latestCommit.Sha, stoppingToken);

                    if (treeResponse == null || treeResponse.Tree == null)
                    {
                        logger.LogError("Tree response is null");
                        break;
                    }
                    foreach (GitTreeItem item in treeResponse.Tree)
                    {
                        if (item.Path?.StartsWith("mappings/") == true && item.Path.EndsWith(".json"))
                        {
                            string subject = item.Path
                                            .Replace("mappings/", string.Empty)
                                            .Replace(".json", string.Empty);

                            bool exist = await metadataDbService.SubjectExistsAsync(subject, stoppingToken);
                            if (exist) continue;

                            JsonElement mappingJson = await githubService.GetMappingJsonAsync(latestCommit.Sha, item.Path, stoppingToken);
                            RegistryItem? registryItem = MapRegistryItem(mappingJson);

                            if (registryItem == null) continue;
                            await metadataDbService.AddTokenAsync(registryItem, stoppingToken);
                        }
                    }
                    await metadataDbService.UpsertSyncStateAsync(latestCommit, stoppingToken);
                }
                else
                {
                    List<GitCommit> latestCommitsSince = await GetLatestCommitsSinceAsync(syncState.Date, stoppingToken);
                    foreach (GitCommit commit in latestCommitsSince)
                    {
                        if (string.IsNullOrEmpty(commit.Url)) continue;

                        GitCommit? resolvedCommit = await githubService.GetMappingJsonAsync<GitCommit>(commit.Url, cancellationToken: stoppingToken);
                        if (resolvedCommit is null || string.IsNullOrEmpty(resolvedCommit.Sha) || resolvedCommit.Files is null) continue;

                        foreach (GitCommitFile file in resolvedCommit.Files)
                        {
                            if (file.Filename is not null)
                            {
                                string subject = file.Filename.Replace("mappings/", string.Empty).Replace(".json", string.Empty);

                                try
                                {
                                    JsonElement mappingJson = await githubService.GetMappingJsonAsync(resolvedCommit.Sha, file.Filename, stoppingToken);
                                    RegistryItem? registryItem = MapRegistryItem(mappingJson);
                                    if (registryItem is null) continue;

                                    bool exists = await metadataDbService.SubjectExistsAsync(subject, stoppingToken);
                                    if (exists)
                                    {
                                        await metadataDbService.UpdateTokenAsync(registryItem, stoppingToken);
                                    }
                                    else
                                    {
                                        await metadataDbService.AddTokenAsync(registryItem, stoppingToken);
                                    }
                                }
                                catch
                                {
                                    logger.LogError("Error processing metadata for subject {Subject}", subject);
                                    await metadataDbService.DeleteTokenAsync(subject, stoppingToken);
                                }
                            }
                        }
                        await metadataDbService.UpsertSyncStateAsync(resolvedCommit, stoppingToken);
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while syncing mappings.");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }

    public RegistryItem? MapRegistryItem(JsonElement mappingJson)
    {
        Dictionary<string, JsonElement> registryItem = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(mappingJson.GetRawText())
                        ?? throw new InvalidOperationException("Failed to deserialize mappingJson into a Dictionary.");

        string? subject = registryItem.TryGetValue("subject", out JsonElement subjectElement)
            ? subjectElement.GetString()
            : null;

        string? name = registryItem.TryGetValue("name", out JsonElement nameElement)
            ? nameElement.GetProperty("value").GetString()
            : null;

        string? ticker = registryItem.TryGetValue("ticker", out JsonElement tickerElement)
            ? tickerElement.GetProperty("value").GetString()
            : null;

        string? description = registryItem.TryGetValue("description", out JsonElement descriptionElement)
            ? descriptionElement.GetProperty("value").GetString()
            : null;

        string? policy = registryItem.TryGetValue("policy", out JsonElement policyElement)
            ? policyElement.GetString()
            : null;

        string? url = registryItem.TryGetValue("url", out JsonElement urlElement)
            ? urlElement.GetProperty("value").GetString()
            : null;

        string? logo = registryItem.TryGetValue("logo", out JsonElement logoElement)
            ? logoElement.GetProperty("value").GetString()
            : null;

        int decimals = registryItem.TryGetValue("decimals", out JsonElement decimalsElement)
            ? decimalsElement.GetProperty("value").GetInt32()
            : 0;

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(ticker))
        {
            logger.LogWarning("Invalid token data. Subject, Name, and Ticker cannot be null or empty.");
            return null;
        }

        return new RegistryItem
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
    }

    private async Task<List<GitCommit>> GetLatestCommitsSinceAsync(DateTimeOffset lastSyncDate, CancellationToken stoppingToken)
    {
        List<GitCommit> latestCommitsSince = [];
        int page = 1;

        while (true)
        {
            IEnumerable<GitCommit>? commitPage = await githubService.GetCommitPageAsync(lastSyncDate, page, stoppingToken);
            if (commitPage is null || !commitPage.Any()) break;
            latestCommitsSince.AddRange(commitPage);
            page++;
        }

        return latestCommitsSince;
    }
}
