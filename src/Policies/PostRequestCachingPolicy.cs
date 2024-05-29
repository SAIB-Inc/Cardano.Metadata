using System.Security.Cryptography;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Primitives;

namespace Cardano.Metadata.Policies;

public sealed class PostRequestCachingPolicy : IOutputCachePolicy
{
    public static readonly PostRequestCachingPolicy Instance = new();

    private PostRequestCachingPolicy()
    {
    }

    async ValueTask IOutputCachePolicy.CacheRequestAsync(
        OutputCacheContext context,
        CancellationToken cancellationToken)
    {
        var attemptOutputCaching = AttemptOutputCaching(context);
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = attemptOutputCaching;
        context.AllowCacheStorage = attemptOutputCaching;
        context.AllowLocking = true;

        // Vary by any query by default
        context.CacheVaryByRules.QueryKeys = "*";

        // Include Post Body in Caching
        if (HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            // Read the request body asynchronously
            context.HttpContext.Request.EnableBuffering();
            using var memoryStream = new MemoryStream();
            await context.HttpContext.Request.Body.CopyToAsync(memoryStream, cancellationToken);
            var requestBodyBytes = memoryStream.ToArray();
            var requestBodyHash = SHA256.HashData(requestBodyBytes);
            var hashedString = Convert.ToHexString(requestBodyHash);

            // Reset the request body stream position
            context.HttpContext.Request.Body.Position = 0;

            // Include the hashed request body in the vary-by rules
            context.CacheVaryByRules.VaryByValues.Add("Body", hashedString);
        }
    }

    ValueTask IOutputCachePolicy.ServeFromCacheAsync
        (OutputCacheContext context, CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }

    ValueTask IOutputCachePolicy.ServeResponseAsync
        (OutputCacheContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;

        // Verify existence of cookie headers
        if (!StringValues.IsNullOrEmpty(response.Headers.SetCookie))
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        // Check response code
        if (response.StatusCode != StatusCodes.Status200OK &&
            response.StatusCode != StatusCodes.Status301MovedPermanently)
        {
            context.AllowCacheStorage = false;
            return ValueTask.CompletedTask;
        }

        return ValueTask.CompletedTask;
    }

    private static bool AttemptOutputCaching(OutputCacheContext context)
    {
        // Check if the current request fulfills the requirements
        // to be cached
        var request = context.HttpContext.Request;

        // Verify the method
        if (!HttpMethods.IsGet(request.Method) &&
            !HttpMethods.IsHead(request.Method) &&
            !HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        // Verify existence of authorization headers
        if (!StringValues.IsNullOrEmpty(request.Headers.Authorization) ||
            request.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            return false;
        }

        return true;
    }
}