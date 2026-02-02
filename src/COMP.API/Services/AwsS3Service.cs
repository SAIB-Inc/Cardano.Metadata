using Amazon.S3;
using Amazon.S3.Model;
using COMP.API.Configuration;
using Microsoft.Extensions.Options;

namespace COMP.API.Services;

public class AwsS3Service(
    IAmazonS3 s3Client,
    IOptions<S3Options> s3Options,
    ILogger<AwsS3Service> logger)
{
    private readonly S3Options _options = s3Options.Value;

    public async Task<string> UploadAsync(string key, byte[] content, string? contentType = null, CancellationToken ct = default)
    {
        using MemoryStream stream = new(content);
        PutObjectRequest request = new()
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType ?? "application/octet-stream"
        };

        await s3Client.PutObjectAsync(request, ct);

        string publicUrl = GetPublicUrl(key);
        logger.LogInformation("Uploaded to S3: {Key} ({Size} bytes)", key, content.Length);

        return publicUrl;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            GetObjectMetadataRequest request = new()
            {
                BucketName = _options.BucketName,
                Key = key
            };

            await s3Client.GetObjectMetadataAsync(request, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public string GetPublicUrl(string key)
    {
        if (!string.IsNullOrEmpty(_options.PublicBaseUrl))
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{key}";

        return $"https://{_options.BucketName}.s3.{_options.Region}.amazonaws.com/{key}";
    }
}
