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
        // GET all tokens
        app.MapGet("/cardano-token", async (TokenMetadataDbContext db) =>
        {
            var tokens = await db.TokenMetadata.ToListAsync();
            if (!tokens.Any())
                return Results.NotFound("No token metadata found.");

            var result = tokens.Select(t => JsonSerializer.Deserialize<JsonElement>(t.Data));
            return Results.Ok(result);
        });

        // GET a specific token by subject
        app.MapGet("/cardano-token/{subject}", async (TokenMetadataDbContext db, string subject) =>
        {
            var token = await db.TokenMetadata.FindAsync(subject);
            if (token is null)
                return Results.NotFound();

            var result = JsonSerializer.Deserialize<JsonElement>(token.Data);
            return Results.Ok(result);
        });

        // POST a new token
        app.MapPost("/cardano-token", async (TokenMetadataDbContext db, TokenMetadata tokenInput) =>
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

        // PUT update an existing token
        app.MapPut("/cardano-token/{subject}", async (TokenMetadataDbContext db, string subject, TokenMetadata tokenInput) =>
        {
            var token = await db.TokenMetadata.FindAsync(subject);
            if (token is null)
                return Results.NotFound();

            var newToken = new TokenMetadata
            {
                Name = tokenInput.Name,
                Description = tokenInput.Description,
                Policy = tokenInput.Policy,
                Ticker = tokenInput.Ticker,
                Url = tokenInput.Url,
                Logo = tokenInput.Logo,
                Decimals = tokenInput.Decimals,
                Data = tokenInput.Data
            };

            await db.SaveChangesAsync();
            return Results.Ok(token);
        });

        // DELETE a token
        app.MapDelete("/cardano-token/{subject}", async (TokenMetadataDbContext db, string subject) =>
        {
            var token = await db.TokenMetadata.FindAsync(subject);
            if (token is null)
                return Results.NotFound();

            db.TokenMetadata.Remove(token);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
