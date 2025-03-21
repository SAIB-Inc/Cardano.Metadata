using System.Net.Http.Headers;
using System.Text.Json;
using Cardano.Metadata.Models.Github;


namespace Cardano.Metadata.Services;

public class GithubService
{
    private readonly ILogger<GithubService> _logger;
    private readonly string _registryOwner;
    private readonly string _registryRepo;
    private readonly string _githubPat;
    private readonly HttpClient _httpClient;
    private readonly MetadataDbService _metadataDbService;

    public GithubService(
       ILogger<GithubService> logger,
       IConfiguration config,
       IHttpClientFactory httpClientFactory,
       MetadataDbService metadataDbService)
    {
        _logger = logger;
        _metadataDbService = metadataDbService;
        _registryOwner = config["RegistryOwner"] ?? throw new InvalidOperationException("RegistryOwner is not configured.");
        _registryRepo = config["RegistryRepo"] ?? throw new InvalidOperationException("RegistryRepo is not configured.");
        _githubPat = config["GithubPAT"] ?? throw new InvalidOperationException("GithubPAT is not configured.");
        _httpClient = httpClientFactory.CreateClient("Github");
        ConfigureHttpClient(config);
    }

    private void ConfigureHttpClient(IConfiguration config)
    {
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CardanoTokenMetadataService", "1.0"));
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/SAIB-Inc/Cardano.Metadata)"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _githubPat);
    }

    public async Task<IEnumerable<GitCommit>> GetCommitsAsync(CancellationToken cancellationToken)
    {
        var commitsUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/commits";
        var commits = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);
        return commits ?? Enumerable.Empty<GitCommit>();
    }

    public async Task<GitTreeResponse> GetGitTreeAsync(string commitSha, CancellationToken cancellationToken)
    {
        var treeUrl = $"https://api.github.com/repos/{_registryOwner}/{_registryRepo}/git/trees/{commitSha}?recursive=true";
        return await _httpClient.GetFromJsonAsync<GitTreeResponse>(treeUrl, cancellationToken)
            ?? throw new InvalidOperationException("GitTreeResponse is null.");
    }

    private async Task<JsonElement> GetMappingJsonAsync(string rawUrl, CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: cancellationToken);
    }

    public async Task ProcessMappingsAsync(string commitSha, CancellationToken cancellationToken)
    {
        var treeResponse = await GetGitTreeAsync(commitSha, cancellationToken);

        if (treeResponse?.Tree != null)
        {
            foreach (var item in treeResponse.Tree)
            {
                if (item.Path?.StartsWith("mappings/") == true && item.Path.EndsWith(".json"))
                {
                    var subject = item.Path.Replace("mappings/", string.Empty).Replace(".json", string.Empty);
                    var rawUrl = $"https://raw.githubusercontent.com/{_registryOwner}/{_registryRepo}/{commitSha}/{item.Path}";
                    if (string.IsNullOrEmpty(commitSha))
                    {
                        _logger.LogWarning("Commit SHA is null or empty for file with subject {subject}. Skipping processing.", subject);
                        return;
                    }

                    _logger.LogInformation("Processing mapping file for subject: {subject}", subject);

                    var mappingJson = await GetMappingJsonAsync(rawUrl, cancellationToken);

                    await _metadataDbService.GetOrCreateTokenAsync(mappingJson, subject, cancellationToken);

                }
            }
        }
        else
        {
            _logger.LogError("No mappings found in the repository.");
        }


    }

}
