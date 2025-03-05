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

        // POST a new token
        app.MapPost("/metadata", async (TokenMetadataDbContext db, TokenMetadata tokenInput) =>
        {
            var existingToken = await db.TokenMetadata.FindAsync(tokenInput.Subject);
            if (existingToken is not null)
                return Results.Conflict($"A token with subject '{tokenInput.Subject}' already exists.");

            var token = new TokenMetadata
            {
                Subject = tokenInput.Subject,
                Name = tokenInput.Name,
                Description = tokenInput.Description,
                Policy = tokenInput.Policy,
                Ticker = tokenInput.Ticker,
                Url = tokenInput.Url,
                Logo = tokenInput.Logo,
                Decimals = tokenInput.Decimals,
                Data = tokenInput.Data
            };

            await db.TokenMetadata.AddAsync(token);
            await db.SaveChangesAsync();

            return Results.Created($"/cardano-token/{token.Subject}", token);
        });
    }
}
