using Carter;
using Metadata.Models.Entity;
using Metadata.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

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

        // POST endpoint for advanced querying
        app.MapPost("/metadata", async (
            TokenMetadataDbContext db,
            [FromBody] List<string> subjects,
            int? limit,
            string? searchKey,
            string? policyId,
            int offset = 0,
            bool includeEmptyName = false,
            bool includeEmptyLogo = false,
            bool includeEmptyTicker = false) =>
        {
            if (subjects is null || subjects.Count == 0)
                return Results.BadRequest("No subjects provided.");

            IQueryable<TokenMetadata> query = db.TokenMetadata;

            if (!string.IsNullOrWhiteSpace(policyId))
            {
                query = query.Where(t => EF.Functions.ILike(t.Subject, policyId + "%"));
            }

            if (!includeEmptyName)
                query = query.Where(t => !string.IsNullOrEmpty(t.Name));
            if (!includeEmptyLogo)
                query = query.Where(t => !string.IsNullOrEmpty(t.Logo));
            if (!includeEmptyTicker)
                query = query.Where(t => !string.IsNullOrEmpty(t.Ticker));

            if (subjects.Count == 1)
            {
                var s = subjects[0].ToLower();
                query = query.Where(t => EF.Functions.ILike(t.Subject, s));
            }
            else
            {
                var orQuery = query.Where(t => false);
                foreach (var subj in subjects)
                {
                    var s = subj.ToLower();
                    orQuery = orQuery.Union(query.Where(t => EF.Functions.ILike(t.Subject, s)));
                }
                query = orQuery;
            }
            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                var pattern = $"%{searchKey.ToLower()}%";
                query = query.Where(t =>
                    EF.Functions.ILike(t.Name, pattern) ||
                    EF.Functions.ILike(t.Ticker, pattern)
                );
            }

            int total = await query.CountAsync();
            query = query.OrderBy(t => t.Ticker)
                         .Skip(offset);

            if (limit.HasValue)
                query = query.Take(limit.Value);

            var tokens = await query.ToListAsync();

            if (tokens.Count == 0)
                return Results.NotFound("No tokens found for the given subjects.");

            return Results.Ok(new { total, data = tokens });
        });
    }
}