using COMP.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Argus.Sync.Data;

namespace COMP.Data.Data;

public class MetadataDbContext(DbContextOptions<MetadataDbContext> options, IConfiguration configuration) : CardanoDbContext(options, configuration)
{
    public DbSet<TokenMetadataRegistry> TokenMetadataRegistry => Set<TokenMetadataRegistry>();
    public DbSet<RegistrySyncState> RegistrySyncState => Set<RegistrySyncState>();
    public DbSet<TokenMetadataOnChain> TokenMetadataOnChain => Set<TokenMetadataOnChain>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TokenMetadataRegistry>().HasKey(tmd => tmd.Subject);
        modelBuilder.Entity<TokenMetadataRegistry>()
        .HasIndex(tmd => new { tmd.Name, tmd.Description, tmd.Ticker })
        .HasDatabaseName("IX_TokenMetadataRegistry_Name_Description_Ticker");

        modelBuilder.Entity<RegistrySyncState>().HasKey(ss => ss.Hash);

        modelBuilder.Entity<TokenMetadataOnChain>(entity =>
        {
            entity.HasKey(e => e.Subject);
            entity.HasIndex(e => e.PolicyId);
        });
    }
}
