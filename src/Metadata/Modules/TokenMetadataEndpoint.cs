using Carter;
using Metadata.Models.Entity; // adjust if needed
using Metadata.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Metadata.Modules;

public class TokenMetadataEndpoint : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {

        // GET a specific token by subject
        app.MapGet("/metadata/{subject}", async (TokenMetadataDbContext db, string subject) =>
        {
            var token = await db.TokenMetadata.FindAsync(subject);
            if (token is null)
                return Results.NotFound();

            var result = JsonSerializer.Deserialize<JsonElement>(token.Data);
            return Results.Ok(result);
        });

        app.MapPost("/metadata", async (TokenMetadataDbContext db, List<string> subjects) =>
        {
            if (subjects == null || subjects.Count == 0)
                return Results.BadRequest("No subjects provided.");

            var lowerSubjects = subjects.Select(s => s.ToLowerInvariant()).ToList();

            var tokens = await db.TokenMetadata
                .Where(t => lowerSubjects.Contains(t.Subject.ToLower()))
                .ToListAsync();

            if (!tokens.Any())
                return Results.NotFound("No tokens found for the given subjects.");

            return Results.Ok(tokens);
        });
    }
}
