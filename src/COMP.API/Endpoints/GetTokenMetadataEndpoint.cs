using FastEndpoints;
using COMP.API.Handlers;
using COMP.Data.Models.Request;

namespace COMP.API.Endpoints;

public class GetTokenMetadataEndpoint(MetadataHandler metadataHandler) : Endpoint<GetTokenMetadataRequest>
{
    private readonly MetadataHandler _metadataHandler = metadataHandler;

    public override void Configure()
    {
        Get("/metadata/{subject}");
        AllowAnonymous();
        Description(b => b
            .WithName("GetTokenMetadata")
            .WithSummary("Retrieve token metadata by subject"));
    }

    public override async Task HandleAsync(GetTokenMetadataRequest req, CancellationToken ct)
    {
        IResult result = await _metadataHandler.GetTokenMetadataAsync(req.Subject, ct);
        await result.ExecuteAsync(HttpContext);
    }
}
