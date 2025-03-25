using Cardano.Metadata.Data;
using Cardano.Metadata.Models.Entity;
using Cardano.Metadata.Services;
using Microsoft.EntityFrameworkCore;

public class GithubWorker(
    ILogger<GithubWorker> logger,
    IDbContextFactory<MetadataDbContext> dbContextFactory,
    GithubService githubService,
    MetadataDbService metadataDbService) : BackgroundService
{
    private readonly ILogger<GithubWorker> _logger = logger;

    private readonly IDbContextFactory<MetadataDbContext> _dbContextFactory = dbContextFactory;
    private readonly GithubService _githubService = githubService;
    private readonly MetadataDbService _metadataDbService = metadataDbService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        _logger.LogInformation("Syncing Mappings");

        var dbContext = await _dbContextFactory.CreateDbContextAsync(stoppingToken);

        var syncState = await dbContext.SyncState
            .OrderByDescending(ss => ss.Date)
            .FirstOrDefaultAsync(cancellationToken: stoppingToken);

        if (syncState is null)
        {
            _logger.LogWarning("No Sync State Information, syncing all mappings...");
            var commits = await _githubService.GetCommitsAsync(stoppingToken);
            var latestCommit = commits.FirstOrDefault();
            if (latestCommit == null || string.IsNullOrEmpty(latestCommit.Sha))
            {
                _logger.LogError("Commit SHA is null or empty for the latest commit.");
                return;
            }
            var treeResponse = await _githubService.GetGitTreeAsync(latestCommit.Sha, stoppingToken);
            if (treeResponse?.Tree != null)
            {
                foreach (var item in treeResponse.Tree)
                {
                    if (item.Path?.StartsWith("mappings/") == true && item.Path.EndsWith(".json"))
                    {
                        if (string.IsNullOrEmpty(latestCommit.Sha))
                        {
                            return;
                        }
                        var rawUrl = _githubService.GetRawFileLink(latestCommit.Sha, item.Path);
                        var mappingJson = await _githubService.GetMappingJsonAsync(rawUrl, stoppingToken);
                        await _metadataDbService.CreateTokenAsync(mappingJson, stoppingToken);
                    }
                }
                await dbContext.SyncState.AddAsync(new SyncState(
                                latestCommit.Sha ?? string.Empty,
                                latestCommit.Commit?.Author?.Date ?? DateTimeOffset.UtcNow
                            ), stoppingToken);

                await dbContext.SaveChangesAsync(stoppingToken);
            }
            else
            {
                _logger.LogError("No mappings found in the repository.");
            }
        }

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
    }
}
