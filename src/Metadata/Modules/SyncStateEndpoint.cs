using Carter;
using Metadata.Models.Entity;
using Metadata.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Metadata.Modules
{
    public class SyncStateEndpoint : CarterModule
    {
        public override void AddRoutes(IEndpointRouteBuilder app)
        {
            // GET all sync states
            app.MapGet("/sync", async (TokenMetadataDbContext db) =>
            {
                var states = await db.SyncState.ToListAsync();
                if (states == null || !states.Any())
                    return Results.NotFound("No sync states found.");

                return Results.Ok(states);
            });

            // GET a specific sync state by SHA
            app.MapGet("/sync/{sha}", async (TokenMetadataDbContext db, string sha) =>
            {
                var state = await db.SyncState.FindAsync(sha);
                if (state is null)
                    return Results.NotFound();

                return Results.Ok(state);
            });

            // POST a new sync state
            app.MapPost("/sync", async (TokenMetadataDbContext db, SyncState sync) =>
            {
                var existingSync = await db.SyncState.FindAsync(sync.Sha);
                if (existingSync is not null)
                    return Results.Conflict($"A sync state with SHA '{sync.Sha}' already exists.");

                await db.SyncState.AddAsync(sync);
                await db.SaveChangesAsync();
                return Results.Created($"/sync/{sync.Sha}", sync);
            });

            // Batch POST to add multiple sync states
            app.MapPost("/sync/batch", async (TokenMetadataDbContext db, List<SyncState> syncStates) =>
            {
                var existingShas = await db.SyncState
                    .Where(s => syncStates.Select(x => x.Sha).Contains(s.Sha))
                    .ToListAsync();

                if (existingShas.Any())
                {
                    var existingShasList = existingShas.Select(x => x.Sha).ToList();
                    return Results.Conflict($"The following sync states already exist: {string.Join(", ", existingShasList)}");
                }

                await db.SyncState.AddRangeAsync(syncStates);
                await db.SaveChangesAsync();
                return Results.Created("/sync/batch", syncStates);
            });


            // DELETE a sync state
            app.MapDelete("/sync/{sha}", async (TokenMetadataDbContext db, string sha) =>
            {
                var state = await db.SyncState.FindAsync(sha);
                if (state is null)
                    return Results.NotFound();

                db.SyncState.Remove(state);
                await db.SaveChangesAsync();
                return Results.NoContent();
            });
        }
    }
}
