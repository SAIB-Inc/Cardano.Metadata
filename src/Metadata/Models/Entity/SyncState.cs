
namespace Metadata.Models.Entity;

public record SyncState
{
    public string Sha { get; init; } = string.Empty;
    public DateTimeOffset Date { get; init; }
}