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

        var syncState = await metadataDbService.GetSyncStateAsync(stoppingToken);

        if (syncState is null)
        {
            logger.LogWarning("No Sync State Information, syncing all mappings...");
            var latestCommit = await githubService.GetCommitsAsync(stoppingToken);

            if (latestCommit == null || string.IsNullOrEmpty(latestCommit.Sha))
            {
                logger.LogError("Commit SHA is null or empty for the latest commit.");
                return;
            }
            var treeResponse = await githubService.GetGitTreeAsync(latestCommit.Sha, stoppingToken);
            if (treeResponse?.Tree != null)
            {
                foreach (var item in treeResponse.Tree)
                {
                    if (item.Path?.StartsWith("mappings/") == true && item.Path.EndsWith(".json"))
                    {
                        var mappingJson = await githubService.GetMappingJsonAsync(latestCommit.Sha, item.Path, stoppingToken);
                        await metadataDbService.AddTokenAsync(mappingJson, stoppingToken);
                    }
                }
                await metadataDbService.AddOrUpdateSyncStateAsync(latestCommit, stoppingToken);
            }
            else
            {
                logger.LogError("No mappings found in the repository.");
            }
        }
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
}
