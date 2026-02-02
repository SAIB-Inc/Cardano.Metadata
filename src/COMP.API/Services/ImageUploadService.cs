using System.Security.Cryptography;
using System.Text.RegularExpressions;
using COMP.API.Configuration;
using COMP.API.Services;
using HeyRed.Mime;
using Microsoft.Extensions.Options;

namespace COMP.API.Services;

public partial class ImageUploadService(
    IOptions<S3Options> s3Options,
    IHttpClientFactory httpClientFactory,
    ILogger<ImageUploadService> logger,
    AwsS3Service? s3Service = null)
{
    private readonly S3Options _options = s3Options.Value;
    private readonly SemaphoreSlim _semaphore = new(s3Options.Value.MaxParallelUploads > 0 ? s3Options.Value.MaxParallelUploads : 4);

    public void TryEnqueueUpload(string subject, string? logoUri)
    {
        if (!_options.Enabled || s3Service is null || string.IsNullOrEmpty(logoUri))
            return;

        _ = UploadAsync(subject, logoUri);
    }

    private async Task UploadAsync(string subject, string logoUri)
    {
        await _semaphore.WaitAsync();
        try
        {
            (byte[]? imageData, string? contentType, string? error) = await ResolveImageAsync(logoUri, CancellationToken.None);

            if (imageData is null || error is not null)
            {
                logger.LogWarning("Failed to resolve image for {Subject}: {Error}", subject, error);
                return;
            }

            string extension = GetExtensionFromContentType(contentType);
            string s3Key = $"{GetKeyFromUri(logoUri, imageData)}{extension}";

            // Check if already exists before uploading
            if (await s3Service!.ExistsAsync(s3Key))
            {
                logger.LogDebug("Image already exists for {Subject}: {Key}", subject, s3Key);
                return;
            }

            await s3Service.UploadAsync(s3Key, imageData, contentType);
            logger.LogInformation("Uploaded image for {Subject}: {S3Key}", subject, s3Key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload image for subject {Subject}", subject);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<(byte[]? Data, string? ContentType, string? Error)> ResolveImageAsync(string logoUri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(logoUri))
            return (null, null, "Empty logo URI");

        if (logoUri.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            return DecodeBase64DataUri(logoUri);

        if (logoUri.StartsWith("ipfs://", StringComparison.OrdinalIgnoreCase))
            return await FetchFromIpfsAsync(logoUri, ct);

        if (logoUri.StartsWith("ar://", StringComparison.OrdinalIgnoreCase))
            return await FetchFromArweaveAsync(logoUri, ct);

        if (logoUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            logoUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return await FetchFromUrlAsync(logoUri, ct);

        return (null, null, $"Unsupported URI scheme: {logoUri}");
    }

    private static (byte[]? Data, string? ContentType, string? Error) DecodeBase64DataUri(string dataUri)
    {
        Match match = DataUriRegex().Match(dataUri);
        if (!match.Success)
            return (null, null, "Invalid data URI format");

        string contentType = match.Groups[1].Value;
        string base64Data = match.Groups[2].Value;

        try
        {
            byte[] data = Convert.FromBase64String(base64Data);
            return (data, contentType, null);
        }
        catch (FormatException)
        {
            return (null, null, "Invalid base64 data");
        }
    }

    private async Task<(byte[]? Data, string? ContentType, string? Error)> FetchFromIpfsAsync(string ipfsUri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.IpfsGateway))
            return (null, null, "IPFS gateway not configured");

        string cid = ipfsUri[7..];
        string gatewayUrl = _options.IpfsGateway.TrimEnd('/') + "/" + cid;

        return await FetchFromUrlAsync(gatewayUrl, ct);
    }

    private async Task<(byte[]? Data, string? ContentType, string? Error)> FetchFromArweaveAsync(string arUri, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ArweaveGateway))
            return (null, null, "Arweave gateway not configured");

        string txId = arUri[5..];
        string gatewayUrl = _options.ArweaveGateway.TrimEnd('/') + "/" + txId;

        return await FetchFromUrlAsync(gatewayUrl, ct);
    }

    private async Task<(byte[]? Data, string? ContentType, string? Error)> FetchFromUrlAsync(string url, CancellationToken ct)
    {
        try
        {
            HttpClient client = httpClientFactory.CreateClient("ImageFetch");
            using HttpResponseMessage response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return (null, null, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

            byte[] data = await response.Content.ReadAsByteArrayAsync(ct);
            string? contentType = response.Content.Headers.ContentType?.MediaType;

            // Fallback to byte detection if content type is missing or generic
            if (string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream")
                contentType = MimeGuesser.GuessMimeType(data);

            return (data, contentType, null);
        }
        catch (HttpRequestException ex)
        {
            return (null, null, $"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return (null, null, "Request timed out");
        }
    }

    private static string GetKeyFromUri(string logoUri, byte[] imageData)
    {
        // Use the CID directly from IPFS URIs
        if (logoUri.StartsWith("ipfs://", StringComparison.OrdinalIgnoreCase))
            return logoUri[7..];

        // Use the transaction ID directly from Arweave URIs
        if (logoUri.StartsWith("ar://", StringComparison.OrdinalIgnoreCase))
            return logoUri[5..];

        // For other sources (base64, http), use content hash
        byte[] hash = SHA256.HashData(imageData);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetExtensionFromContentType(string? contentType) =>
        contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/bmp" => ".bmp",
            "image/x-icon" => ".ico",
            "image/avif" => ".avif",
            _ => string.Empty
        };

    [GeneratedRegex(@"^data:(image/[^;]+);base64,(.+)$", RegexOptions.Singleline)]
    private static partial Regex DataUriRegex();
}
