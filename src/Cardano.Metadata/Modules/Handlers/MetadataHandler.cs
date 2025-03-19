using Cardano.Metadata.Data;
using Cardano.Metadata.Models.Entity;
using Microsoft.EntityFrameworkCore;
using LinqKit;
using Cardano.Metadata.Models.Response;

namespace Cardano.Metadata.Modules.Handlers
{
    public class MetadataHandler(
        IDbContextFactory<MetadataDbContext> _dbContextFactory
    )
    {
        // Fetch data by subject
        public async Task<IResult> GetTokenMetadataAsync(string subject)
        {
            using var db = await _dbContextFactory.CreateDbContextAsync();
            var token = await db.TokenMetadata
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Subject == subject);

            if (token is null)
                return Results.NotFound();

            var metadataResponse = new MetadataResponse
            {
                Subject = token.Subject ?? string.Empty,
                Decimals = new ValueResponse<int> { Value = token.Decimals ?? 0 }, 
                Description = token.Description is not null
                    ? new ValueResponse<string> { Value = token.Description }
                    : null,
                Name = new ValueResponse<string> { Value = token.Name ?? string.Empty},
                Ticker = new ValueResponse<string> { Value = token.Ticker ?? string.Empty},
                Url = new ValueResponse<string> { Value = token.Url ?? string.Empty},
                Logo = new ValueResponse<string> { Value = token.Logo ?? string.Empty}
            };

            return Results.Ok(metadataResponse);
        }

        // Fetch data by batch with additional filtering
        public async Task<IResult> BatchTokenMetadataAsync(
            List<string> subjects,
            int? limit,
            string? searchKey,
            string? policyId,
            int offset = 0,
            bool includeEmptyName = false,
            bool includeEmptyLogo = false,
            bool includeEmptyTicker = false)
        {
            if (subjects is null || subjects.Count == 0)
                return Results.BadRequest("No subjects provided.");

            using var db = await _dbContextFactory.CreateDbContextAsync();

            var distinctSubjects = subjects.Distinct().ToList();
            var predicate = PredicateBuilder.New<TokenMetadata>(false);

            foreach (var subject in distinctSubjects)
            {
                predicate = predicate.Or(token => token.Subject == subject);
            }

            if (!string.IsNullOrWhiteSpace(policyId))
            {
                predicate = predicate.And(token =>
                    token.Subject != null &&
                    token.Subject.Substring(0, 56).Equals(policyId, StringComparison.CurrentCultureIgnoreCase));
            }

            if (!includeEmptyName)
            {
                predicate = predicate.And(token => !string.IsNullOrEmpty(token.Name));
            }

            if (!includeEmptyLogo)
            {
                predicate = predicate.And(token => !string.IsNullOrEmpty(token.Logo));
            }

            if (!includeEmptyTicker)
            {
                predicate = predicate.And(token => !string.IsNullOrEmpty(token.Ticker));
            }

            if (!string.IsNullOrWhiteSpace(searchKey))
            {
                var lowerSearchKey = searchKey.ToLower();
                var searchPredicate = PredicateBuilder.New<TokenMetadata>(false);
                searchPredicate = searchPredicate.Or(token => token.Name != null && EF.Functions.Like(token.Name.ToLower(), lowerSearchKey + "%"));
                searchPredicate = searchPredicate.Or(token => token.Description != null && EF.Functions.Like(token.Description.ToLower(), lowerSearchKey + "%"));
                searchPredicate = searchPredicate.Or(token => token.Ticker != null && EF.Functions.Like(token.Ticker.ToLower(), lowerSearchKey + "%"));
                predicate = predicate.And(searchPredicate);
            }

            IQueryable<TokenMetadata> query = db.TokenMetadata
                .AsNoTracking()
                .Where(predicate);

            int total = await query.CountAsync();

            if (limit.HasValue)
                query = query.Skip(offset).Take(limit.Value);

            var tokenList = await query.ToListAsync();

            if (tokenList.Count == 0)
                return Results.NotFound("No tokens found for the given subjects.");
            
            var metadataResponses = tokenList.Select(token => new MetadataResponse
            {
                Subject = token.Subject ?? string.Empty,
                Decimals = new ValueResponse<int> { Value = token.Decimals ?? 0 },
                Description = token.Description is not null
                    ? new ValueResponse<string> { Value = token.Description }
                    : null,
                Name = new ValueResponse<string> { Value = token.Name ?? string.Empty },
                Ticker = new ValueResponse<string> { Value = token.Ticker ?? string.Empty },
                Url = new ValueResponse<string> { Value = token.Url ?? string.Empty },
                Logo = new ValueResponse<string> { Value = token.Logo ?? string.Empty } 
            }).ToList();

            return Results.Ok(new { total, data = metadataResponses });
        }
    }
}
