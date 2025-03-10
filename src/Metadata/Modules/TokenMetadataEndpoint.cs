using Carter;
using Metadata.Models.Entity;
using Metadata.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using LinqKit;
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

        //BATCH Query with different params for advance query
        app.MapPost("/metadata", async (
            TokenMetadataDbContext db,
            [FromBody] List<string> subjects,
            int? limit,
            string? searchKey) =>
        {
            if (subjects is null || subjects.Count == 0)
                return Results.BadRequest("No subjects provided.");

            var lowerSubjects = subjects.Select(s => s.ToLower()).ToList();

            var predicate = PredicateBuilder.New<TokenMetadata>(true);

            predicate = predicate.And(token => lowerSubjects.Contains(token.Subject.ToLower()));

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                var searchPredicate = PredicateBuilder.New<TokenMetadata>(false);
                searchPredicate = searchPredicate.Or(token => token.Name.Contains(searchKey));
                searchPredicate = searchPredicate.Or(token => token.Description.Contains(searchKey));
                searchPredicate = searchPredicate.Or(token => token.Ticker.Contains(searchKey));

                predicate = predicate.And(searchPredicate);
            }

            IQueryable<TokenMetadata> query = db.TokenMetadata.Where(predicate);


            int total = await query.CountAsync();

            if (limit.HasValue)
                query = query.Take(limit.Value);

            var tokens = await query.ToListAsync();

            if (tokens.Count == 0)
                return Results.NotFound("No tokens found for the given subjects.");

            return Results.Ok(new { total, data = tokens });
        });

    }
}