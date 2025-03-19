using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Cardano.Metadata.Data;
using Cardano.Metadata.Models.Github;

namespace Cardano.Metadata.Workers.Handlers
{
    public class SyncHandler
    {
        private readonly ILogger<Github> _logger;
        private readonly string _registryOwner;
        private readonly string _registryRepo;
        private readonly string _rawBaseUrl;
        private readonly string _githubPat;
        private readonly HttpClient _httpClient;
        private readonly DatabaseHandler _databaseHandler;

        public SyncHandler(
            ILogger<Github> logger,
            IConfiguration config,
            IHttpClientFactory httpClientFactory,
            DatabaseHandler databaseHandler)
        {
            _logger = logger;
            _registryOwner = config["RegistryOwner"] ?? throw new InvalidOperationException("RegistryOwner is not configured.");
            _registryRepo = config["RegistryRepo"] ?? throw new InvalidOperationException("RegistryRepo is not configured.");
            _rawBaseUrl = config["RawBaseUrl"] ?? throw new InvalidOperationException("RawBaseUrl is not configured.");
            _githubPat = config["GithubPAT"] ?? throw new InvalidOperationException("GithubPAT is not configured.");
            _httpClient = httpClientFactory.CreateClient("Github");
            _databaseHandler = databaseHandler;
            ConfigureHttpClient(config);
        }

        private void ConfigureHttpClient(IConfiguration config)
        {   
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            var productVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "Unknown Version";
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CardanoTokenMetadataService", productVersion));
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/SAIB-Inc/Cardano.Metadata)"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _githubPat);
        }

        public async Task ProcessFullSyncAsync(MetadataDbContext dbContext, CancellationToken cancellationToken)
        {
            _logger.LogWarning("No Sync State Information, syncing all mappings...");
            var commits = await GetCommitsAsync(_registryOwner, _registryRepo, cancellationToken);
            var latestCommit = commits.FirstOrDefault();
            if (latestCommit == null || string.IsNullOrEmpty(latestCommit.Sha))
            {
                _logger.LogError("No valid commit found for repository.");
                return;
            }

            var treeResponse = await GetGitTreeAsync(_registryOwner, _registryRepo, latestCommit.Sha, cancellationToken);
            if (treeResponse?.Tree != null)
            {
                foreach (var item in treeResponse.Tree)
                {
                    if (item.Path?.StartsWith("mappings/") == true && item.Path.EndsWith(".json"))
                    {
                        var subject = item.Path.Replace("mappings/", string.Empty).Replace(".json", string.Empty);
                        var rawUrl = $"{_rawBaseUrl}/{_registryOwner}/{_registryRepo}/{latestCommit.Sha}/{item.Path}";
                        await ProcessMappingFileAsync(rawUrl, subject, latestCommit.Sha, dbContext, cancellationToken);
                    }
                }
            }
            else
            {
                _logger.LogError("No mappings found in the repository.");
            }
        }

        private async Task<IEnumerable<GitCommit>> GetCommitsAsync(string registryOwner, string registryRepo, CancellationToken cancellationToken)
        {
            var commitsUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/commits";
            var commits = await _httpClient.GetFromJsonAsync<IEnumerable<GitCommit>>(commitsUrl, cancellationToken);
            return commits ?? Enumerable.Empty<GitCommit>();
        }

        private async Task<GitTreeResponse> GetGitTreeAsync(string registryOwner, string registryRepo, string commitSha, CancellationToken cancellationToken)
        {
            var treeUrl = $"https://api.github.com/repos/{registryOwner}/{registryRepo}/git/trees/{commitSha}?recursive=true";
            return await _httpClient.GetFromJsonAsync<GitTreeResponse>(treeUrl, cancellationToken)
                   ?? throw new InvalidOperationException("GitTreeResponse is null.");
        }

        private async Task ProcessMappingFileAsync(string rawUrl, string subject, string commitSha, MetadataDbContext dbContext, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(commitSha))
            {
                _logger.LogWarning("Commit SHA is null or empty for file with subject {subject}. Skipping processing.", subject);
                return;
            }

            _logger.LogInformation("Processing mapping file for subject: {subject}", subject);

            var mappingJson = await GetMappingJsonAsync(rawUrl, cancellationToken);
            var mappingBytes = await GetMappingBytesAsync(rawUrl, cancellationToken);

            var token = await _databaseHandler.GetOrCreateTokenAsync(mappingJson, mappingBytes, subject, cancellationToken);

        }

        private async Task<JsonElement> GetMappingJsonAsync(string rawUrl, CancellationToken cancellationToken)
        {
            return await _httpClient.GetFromJsonAsync<JsonElement>(rawUrl, cancellationToken: cancellationToken);
        }

        private async Task<byte[]> GetMappingBytesAsync(string rawUrl, CancellationToken cancellationToken)
        {
            return await _httpClient.GetByteArrayAsync(rawUrl, cancellationToken);
        }
    }
}
