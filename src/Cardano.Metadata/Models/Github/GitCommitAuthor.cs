namespace Cardano.Metadata.Models.Github;

public record GitCommitAuthor
(
    string? Name,
    string? Email,
    DateTime? Date
);