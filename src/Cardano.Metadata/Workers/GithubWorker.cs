using Cardano.Metadata.Data;
using Cardano.Metadata.Services;
using Microsoft.EntityFrameworkCore;

public class GithubWorker(
    ILogger<GithubWorker> logger,
    IDbContextFactory<MetadataDbContext> dbContextFactory,
    GithubService githubService) : BackgroundService
{
    private readonly ILogger<GithubWorker> _logger = logger;

    private readonly IDbContextFactory<MetadataDbContext> _dbContextFactory = dbContextFactory;
    private readonly GithubService _githubService = githubService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
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
                    await _githubService.ProcessMappingsAsync(latestCommit.Sha, stoppingToken);
                }
                else
                {
                    _logger.LogError("No mappings found in the repository.");
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
