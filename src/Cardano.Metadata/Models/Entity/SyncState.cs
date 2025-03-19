namespace Cardano.Metadata.Models.Entity;

public record SyncState(string sha, DateTimeOffset date)
{
    public string Sha { get; } = sha ?? string.Empty;
    public DateTimeOffset Date { get; } = date;
}
