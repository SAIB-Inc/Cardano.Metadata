using Cardano.Metadata.Data;
using Cardano.Metadata.Workers.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

public class Github(
    ILogger<Github> logger,
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<MetadataDbContext> dbContextFactory) : BackgroundService
{
    private readonly ILogger<Github> _logger = logger;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IDbContextFactory<MetadataDbContext> _dbContextFactory = dbContextFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Syncing Mappings");

            using (var scope = _scopeFactory.CreateScope()) 
            {
                var syncHandler = scope.ServiceProvider.GetRequiredService<SyncHandler>();
                var dbContext = await _dbContextFactory.CreateDbContextAsync(stoppingToken);

                var syncState = await dbContext.SyncState
                    .OrderByDescending(ss => ss.Date)
                    .FirstOrDefaultAsync(cancellationToken: stoppingToken);

                if (syncState is null)
                {
                    await syncHandler.ProcessFullSyncAsync(dbContext, stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
