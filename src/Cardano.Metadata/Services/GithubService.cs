using System.Text.Json;
using Cardano.Metadata.Models.Github;

namespace Cardano.Metadata.Services;

public class GithubService
(
   IConfiguration config,
   IHttpClientFactory httpClientFactory)
{
    private readonly string _registryOwner = config["RegistryOwner"] ?? throw new InvalidOperationException("RegistryOwner is not configured.");
    private readonly string _registryRepo = config["RegistryRepo"] ?? throw new InvalidOperationException("RegistryRepo is not configured.");
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("GithubApi");

    public async Task<GitCommit?> GetCommitsAsync(CancellationToken cancellationToken)
    {
        var commitsUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/commits";
        var commits = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);
        return commits?.FirstOrDefault();
    }

    public async Task<GitTreeResponse> GetGitTreeAsync(string commitSha, CancellationToken cancellationToken)
    {
        var treeUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/git/trees/{commitSha}?recursive=true";
        return await _httpClient.GetFromJsonAsync<GitTreeResponse>(treeUrl, cancellationToken)
            ?? throw new InvalidOperationException("GitTreeResponse is null.");
    }

    public async Task<JsonElement> GetMappingJsonAsync(string commitSha, string filePath, CancellationToken cancellationToken)
    {
        string rawUrl = $"https://raw.githubusercontent.com/{_registryOwner}/{_registryRepo}/{commitSha}/{filePath}";
        return await _httpClient.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: cancellationToken);
    }

}
