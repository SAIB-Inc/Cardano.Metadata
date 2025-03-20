
using Cardano.Metadata.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Cardano.Metadata.Data;

public class MetadataDbContext(DbContextOptions<MetadataDbContext> options) : DbContext(options)
{
    public DbSet<MetaData> MetaData => Set<MetaData>();
    public DbSet<SyncState> SyncState => Set<SyncState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MetaData>().HasKey(tmd => tmd.Subject);

        modelBuilder.Entity<MetaData>()
        .HasIndex(tmd => new { tmd.Name, tmd.Description, tmd.Ticker })
        .HasDatabaseName("IX_TokenMetadata_Name_Description_Ticker");

        modelBuilder.Entity<SyncState>().HasKey(ss => ss.Sha);
    }
}