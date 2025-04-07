using System.Text.Json;
using Cardano.Metadata.Models.Github;

namespace Cardano.Metadata.Services;

public class GithubService
(
   IConfiguration config,
   ILogger<MetadataDbService> logger,
   IHttpClientFactory httpClientFactory)
{
    private readonly string _registryOwner = config["RegistryOwner"] ?? throw new InvalidOperationException("RegistryOwner is not configured.");
    private readonly string _registryRepo = config["RegistryRepo"] ?? throw new InvalidOperationException("RegistryRepo is not configured.");
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("GithubApi");

    public async Task<GitCommit?> GetCommitsAsync(CancellationToken cancellationToken)
    {
        try
        {
            string commitsUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/commits";
            IEnumerable<GitCommit>? commits = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);
            return commits?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving commits from GitHub.");
            throw;
        }
    }

    public async Task<GitTreeResponse> GetGitTreeAsync(string commitSha, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commitSha))
            throw new ArgumentException("Commit SHA cannot be null or empty.", nameof(commitSha));

        string treeUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/git/trees/{commitSha}?recursive=true";
        try
        {
            GitTreeResponse? gitTreeResponse = await _httpClient.GetFromJsonAsync<GitTreeResponse>(treeUrl, cancellationToken);
            if (gitTreeResponse is null)
                throw new InvalidOperationException("GitTreeResponse is null.");
            return gitTreeResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving Git tree for commit {CommitSha}.", commitSha);
            throw;
        }
    }

    public async Task<JsonElement> GetMappingJsonAsync(string commitSha, string filePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(commitSha))
            throw new ArgumentException("Commit SHA cannot be null or empty.", nameof(commitSha));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));

        string rawUrl = $"https://raw.githubusercontent.com/{_registryOwner}/{_registryRepo}/{commitSha}/{filePath}";
        return await GetMappingJsonAsync<JsonElement>(rawUrl, cancellationToken);
    }

    public async Task<T?> GetMappingJsonAsync<T>(string rawUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            throw new ArgumentException("Raw URL cannot be null or empty.", nameof(rawUrl));

        try
        {
            return await _httpClient.GetFromJsonAsync<T>(rawUrl, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving mapping JSON from URL {RawUrl}.", rawUrl);
            throw;
        }
    }

    public async Task<IEnumerable<GitCommit>?> GetCommitPageAsync(DateTimeOffset lastSync, int page, CancellationToken cancellationToken)
    {
        try
        {
            string commitsUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/commits?since={lastSync.AddSeconds(1):yyyy-MM-dd'T'HH:mm:ssZ}&page={page}";
            IEnumerable<GitCommit>? commitPage = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);
            return commitPage;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving commit page {Page} since {LastSync}.", page, lastSync);
            throw;
        }
    }
}
