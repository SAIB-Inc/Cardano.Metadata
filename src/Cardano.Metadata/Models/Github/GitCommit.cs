namespace Cardano.Metadata.Models.Github;

public record GitCommit
(
    string? Sha,
    string? Url,
    GitCommitInfo? Commit,
    IEnumerable<GitCommitFile>? Files
);