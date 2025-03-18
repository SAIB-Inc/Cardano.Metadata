using Carter;
using Cardano.Metadata.Modules.Handlers;

namespace Cardano.Metadata.Modules
{
    public class MetadataEndpoints(MetadataHandler metadataHandler) : CarterModule
    {
        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/metadata")
                .WithTags("Metadata")
                .WithOpenApi();

            group.MapGet("/{subject}", (string subject) =>
                metadataHandler.GetTokenMetadataAsync(subject))
                .WithName("GetTokenMetadata")
                .WithDescription("Retrieve token metadata by subject");

            group.MapPost("/", (List<string> subjects, int? limit, string? searchKey, string? policyId, int offset, bool includeEmptyName, bool includeEmptyLogo, bool includeEmptyTicker) =>
                metadataHandler.BatchTokenMetadataAsync(subjects, limit, searchKey, policyId, offset, includeEmptyName, includeEmptyLogo, includeEmptyTicker))
                .WithName("BatchTokenMetadata")
                .WithDescription("Retrieve token metadata for a batch of subjects");
        }
    }
}
