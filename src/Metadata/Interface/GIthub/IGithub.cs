using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Metadata.Models.Github;

namespace Metadata.Interface.GIthub;

public interface IGithub
{
    Task<IEnumerable<GitCommit>> GetCommitsAsync(string registryOwner, string registryRepo, CancellationToken cancellationToken);
    Task<GitTreeResponse> GetGitTreeAsync(string registryOwner, string registryRepo, string commitSha, CancellationToken cancellationToken);
    Task<JsonElement> GetMappingJsonAsync(string rawUrl, CancellationToken cancellationToken);
    Task<byte[]> GetMappingBytesAsync(string rawUrl, CancellationToken cancellationToken);
    Task<IEnumerable<GitCommit>> GetCommitsSinceAsync(string registryOwner, string registryRepo, DateTimeOffset since, CancellationToken cancellationToken);
    Task<GitCommit> GetCommitDetailsAsync(string url, CancellationToken cancellationToken);

}

