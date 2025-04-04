using System.Text.Json;
using Cardano.Metadata.Models.Entity;
using Cardano.Metadata.Models.Github;
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
        logger.LogInformation("Syncing Mappings");

        SyncState? syncState = await metadataDbService.GetSyncStateAsync(stoppingToken);

        if (syncState is null)
        {
            logger.LogWarning("No Sync State Information, syncing all mappings...");
            GitCommit? latestCommit = await githubService.GetCommitsAsync(stoppingToken);

            if (latestCommit == null || string.IsNullOrEmpty(latestCommit.Sha))
            {
                logger.LogError("Commit SHA is null or empty for the latest commit.");
                return;
            }
            GitTreeResponse treeResponse = await githubService.GetGitTreeAsync(latestCommit.Sha, stoppingToken);

            if (treeResponse == null || treeResponse.Tree == null)
            {
                logger.LogError("Tree response is null.");
                return;
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
                    await metadataDbService.AddTokenAsync(mappingJson, stoppingToken);
                }
            }
            await metadataDbService.UpsertSyncStateAsync(latestCommit, stoppingToken);
        }
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }

}
