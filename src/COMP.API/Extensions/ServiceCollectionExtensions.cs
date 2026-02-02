using Amazon.S3;
using COMP.API.Configuration;
using COMP.API.Services;

namespace COMP.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddS3ImageUpload(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
        services.Configure<GatewayOptions>(configuration.GetSection(GatewayOptions.SectionName));

        S3Options s3Options = configuration.GetSection(S3Options.SectionName).Get<S3Options>() ?? new();

        if (!s3Options.Enabled)
            return services;

        services.AddSingleton<IAmazonS3>(_ =>
        {
            AmazonS3Config config = new()
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(s3Options.Region)
            };

            if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
            {
                config.ServiceURL = s3Options.ServiceUrl;
                config.ForcePathStyle = true;
            }

            return new AmazonS3Client(s3Options.AccessKey, s3Options.SecretKey, config);
        });

        services.AddSingleton<AwsS3Service>();
        services.AddSingleton<ImageUploadService>();
        services.AddHttpClient("ImageFetch", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "COMP-API/1.0");
        });

        return services;
    }
}
