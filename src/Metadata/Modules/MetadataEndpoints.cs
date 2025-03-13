

using System.Text.Json;
using Carter;
using LinqKit;
using Metadata.Data;
using Metadata.Models.Entity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Metadata.Modules;

public class MetadataEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/metadata");

        //Get Token Metadata by Subject
        group.MapGet("/{subject}", async (MetadataDbContext db, string subject) =>
        {
            var token = await db.TokenMetadata.AsNoTracking().FirstOrDefaultAsync(t => t.Subject == subject);
            if (token is null)
                return Results.NotFound();

            var result = JsonSerializer.Deserialize<JsonElement>(token.Data);
            return Results.Ok(result);
        });

        // Get Token Metadata by Batch
        group.MapPost("", async (MetadataDbContext db, [FromBody] List<string> subjects, int? limit, string? searchKey) =>
        {
            if (subjects is null || subjects.Count == 0)
                return Results.BadRequest("No subjects provided.");

            var distinctSubjects = subjects.Distinct().ToList();

            var predicate = PredicateBuilder.New<TokenMetadata>(false);

            foreach (var subject in distinctSubjects)
            {
                predicate = predicate.Or(token => token.Subject == subject);
            }

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                var lowerSearchKey = searchKey.ToLower();
                var searchPredicate = PredicateBuilder.New<TokenMetadata>(false);
                searchPredicate = searchPredicate.Or(token => EF.Functions.Like(token.Name.ToLower(), lowerSearchKey + "%"));
                searchPredicate = searchPredicate.Or(token => EF.Functions.Like(token.Description.ToLower(), lowerSearchKey + "%"));
                searchPredicate = searchPredicate.Or(token => EF.Functions.Like(token.Ticker.ToLower(), lowerSearchKey + "%"));

                predicate = predicate.And(searchPredicate);
            }

            IQueryable<byte[]> query = db.TokenMetadata.AsNoTracking()
                .Where(predicate)
                .Select(token => token.Data);

            int total = await query.CountAsync();

            if (limit.HasValue)
                query = query.Take(limit.Value);

            var dataList = await query.ToListAsync();

            if (dataList.Count == 0)
                return Results.NotFound("No tokens found for the given subjects.");

            var result = dataList.Select(data =>
            {
                try
                {
                    return JsonSerializer.Deserialize<JsonElement>(data);
                }
                catch (JsonException)
                {
                    return default(JsonElement);
                }
            }).ToList();

            return Results.Ok(new { total, data = result });
        });

    }
}