namespace Cardano.Metadata.Models.Entity;

public record SyncState(
    string Hash,
    DateTimeOffset Date
);