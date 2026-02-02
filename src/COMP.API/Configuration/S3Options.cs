namespace COMP.API.Configuration;

public class S3Options
{
    public const string SectionName = "S3";

    public bool Enabled { get; set; }
    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? ServiceUrl { get; set; }
    public string? PublicBaseUrl { get; set; }
    public int MaxParallelUploads { get; set; } = 4;
}
