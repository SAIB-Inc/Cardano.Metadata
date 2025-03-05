using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Metadata.Models.Github;

public class GitCommit
{
    public string? Sha { get; init; }
    public string? Url { get; init; }
    public GitCommitInfo? Commit { get; init; }
    public IEnumerable<GitCommitFile>? Files { get; init; }
}