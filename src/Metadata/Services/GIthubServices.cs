using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Metadata.Interface.GIthub;
using Metadata.Models.Github;


namespace Metadata.Services;


public class GitHubService : IGithub
{
    private readonly HttpClient _httpClient;


    public GitHubService(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<GitHubService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Github");
        ConfigureHttpClient(config);
    }

    private void ConfigureHttpClient(IConfiguration config)
    {
        var githubPat = config["GithubPAT"] ?? throw new InvalidOperationException("GithubPAT is not configured.");
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        var productVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version";
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CardanoTokenMetadataService", productVersion));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/SAIB-Inc/Cardano.Metadata)"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", githubPat);
    }

    public async Task<IEnumerable<GitCommit>> GetCommitsAsync(string registryOwner, string registryRepo, CancellationToken cancellationToken)
    {
        var commitsUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/commits";
        var commits = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);
        return commits ?? Enumerable.Empty<GitCommit>();
    }

    public async Task<GitTreeResponse> GetGitTreeAsync(string registryOwner, string registryRepo, string commitSha, CancellationToken cancellationToken)
    {
        var treeUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/git/trees/{commitSha}?recursive=true";
        return await _httpClient.GetFromJsonAsync<GitTreeResponse>(treeUrl, cancellationToken)
               ?? throw new InvalidOperationException("GitTreeResponse is null.");
    }



    public async Task<JsonElement> GetMappingJsonAsync(string rawUrl, CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: cancellationToken);
    }

    public async Task<byte[]> GetMappingBytesAsync(string rawUrl, CancellationToken cancellationToken)
    {
        return await _httpClient.GetByteArrayAsync(rawUrl, cancellationToken);
    }

    public async Task<IEnumerable<GitCommit>> GetCommitsSinceAsync(string registryOwner, string registryRepo, DateTimeOffset since, CancellationToken cancellationToken)
    {
        int page = 1;
        var allCommits = new List<GitCommit>();
        var sinceParam = since.AddSeconds(1).ToString("yyyy-MM-dd'T'HH:mm:ssZ");

        while (true)
        {
            var commitsPageUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/commits?since={sinceParam}&page={page}";
            var commitsPage = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsPageUrl, cancellationToken);
            if (commitsPage == null || !commitsPage.Any())
                break;

            allCommits.AddRange(commitsPage);
            page++;
        }
        return allCommits;
    }

    public async Task<GitCommit> GetCommitDetailsAsync(string url, CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<GitCommit>(url, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("GitCommit is null.");
    }


}