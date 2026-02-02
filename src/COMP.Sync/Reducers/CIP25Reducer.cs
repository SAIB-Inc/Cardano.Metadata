using Argus.Sync.Reducers;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Cbor.Extensions.Cardano.Core;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Microsoft.EntityFrameworkCore;
using COMP.Data.Data;
using COMP.Data.Models.Entity;
using System.Text;
using System.Text.RegularExpressions;
using Chrysalis.Cbor.Types.Cardano.Core.Common;

namespace COMP.Sync.Reducers;

public partial class CIP25Reducer(
    IDbContextFactory<MetadataDbContext> dbContextFactory,
    ILogger<CIP25Reducer> logger
) : IReducer<TokenMetadataOnChain>
{
    private static readonly string[] ValidSchemes = ["https://", "http://", "ipfs://", "ar://", "data:"];
    private static readonly Regex DataUriRegex = MyRegex();

    // Rollback is intentionally not implemented - this metadata indexer is append-only
    // and doesn't track historical state. On-chain metadata updates are rare and
    // rollbacks would require re-syncing from scratch.
    public async Task RollBackwardAsync(ulong slot)
    {
        logger.LogWarning("Rollback requested to slot {Slot}. Manual resync may be required.", slot);
        await Task.CompletedTask;
    }

    public async Task RollForwardAsync(Block block)
    {
        List<TransactionBody> txBodies = [.. block.TransactionBodies()];
        Dictionary<int, AuxiliaryData> auxiliaryDataDict = block.AuxiliaryDataSet().ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Extract all potential tokens with CIP-25 metadata
        List<TokenMetadataOnChain?> allTokens = [.. txBodies
            .Select((tx, index) => (tx, index))
            .Where(x => x.tx.Mint() is { Count: > 0 })
            .Where(x => auxiliaryDataDict.TryGetValue(x.index, out AuxiliaryData? aux)
                && aux.Metadata()?.Value().TryGetValue(721, out TransactionMetadatum? cip25) == true
                && cip25 is MetadatumMap)
            .SelectMany(x =>
            {
                MetadatumMap rootMap = (MetadatumMap)auxiliaryDataDict[x.index].Metadata()!.Value()[721];
                return x.tx.Mint()!.SelectMany(policyEntry =>
                {
                    string policyId = Convert.ToHexString(policyEntry.Key).ToLowerInvariant();
                    return policyEntry.Value.Value
                        .Where(a => a.Value > 0)
                        .Select(assetEntry =>
                        {
                            string assetNameHex = Convert.ToHexString(assetEntry.Key).ToLowerInvariant();
                            return ExtractMetadata(rootMap, policyId, assetNameHex, assetEntry.Value);
                        });
                });
            })];

        // Log skipped tokens
        allTokens
            .Where(t => t is not null && (string.IsNullOrEmpty(t.Name) || string.IsNullOrEmpty(t.Logo)))
            .ToList()
            .ForEach(t => logger.LogWarning("Skipping {Subject} - CIP-25 requires name and image fields", t!.Subject));

        // Filter valid tokens
        Dictionary<string, TokenMetadataOnChain> tokensWithMetadata = allTokens
            .Where(t => t is not null && !string.IsNullOrEmpty(t.Name) && !string.IsNullOrEmpty(t.Logo))
            .GroupBy(t => t!.Subject)
            .ToDictionary(g => g.Key, g => g.Last()!);

        if (tokensWithMetadata.Count == 0)
            return;

        await using MetadataDbContext db = await dbContextFactory.CreateDbContextAsync();

        Dictionary<string, TokenMetadataOnChain> existingRecords = await db.TokenMetadataOnChain
            .Where(t => tokensWithMetadata.Keys.Contains(t.Subject))
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Subject);

        List<TokenMetadataOnChain> toInsert = [.. tokensWithMetadata.Values.Where(t => !existingRecords.ContainsKey(t.Subject))];
        List<TokenMetadataOnChain> toUpdate = [.. tokensWithMetadata.Values.Where(t => existingRecords.TryGetValue(t.Subject, out TokenMetadataOnChain? existing) && !TokensAreEqual(t, existing))];

        db.TokenMetadataOnChain.AddRange(toInsert);
        db.TokenMetadataOnChain.UpdateRange(toUpdate);

        toInsert.ForEach(t => logger.LogInformation("Inserted {Subject} with quantity: {Quantity}", t.Subject, t.Quantity));
        toUpdate.ForEach(t => logger.LogInformation("Updated {Subject} - quantity: {OldQty} -> {NewQty}",
            t.Subject, existingRecords[t.Subject].Quantity, t.Quantity));

        await db.SaveChangesAsync();
    }

    private static TokenMetadataOnChain? ExtractMetadata(
        MetadatumMap rootMap,
        string policyId,
        string assetNameHex,
        long quantity)
    {
        bool isVersion2 = rootMap.Value.Any(kvp => kvp.Key is MetadatumBytes);

        KeyValuePair<TransactionMetadatum, TransactionMetadatum> policyKvp = rootMap.Value.FirstOrDefault(kvp =>
        {
            string? policy = kvp.Key switch
            {
                MetadataText text => text.Value?.ToLowerInvariant(),
                MetadatumBytes bytes => Convert.ToHexString(bytes.Value).ToLowerInvariant(),
                _ => null
            };
            return policy == policyId;
        });

        if (policyKvp.Value is not MetadatumMap policyMap)
            return null;

        KeyValuePair<TransactionMetadatum, TransactionMetadatum> assetKvp = policyMap.Value.FirstOrDefault(kvp =>
        {
            if (isVersion2)
            {
                if (kvp.Key is MetadatumBytes bytes)
                {
                    string hexBytes = Convert.ToHexString(bytes.Value).ToLowerInvariant();
                    return hexBytes == assetNameHex;
                }
            }
            else
            {
                if (kvp.Key is MetadataText textKey && !string.IsNullOrEmpty(textKey.Value))
                {
                    try
                    {
                        string hexFromUtf8 = Convert.ToHexString(Encoding.UTF8.GetBytes(textKey.Value)).ToLowerInvariant();
                        return hexFromUtf8 == assetNameHex;
                    }
                    catch { }
                }
            }

            return false;
        });

        if (assetKvp.Value is not MetadatumMap assetMap)
            return null;

        Dictionary<string, TransactionMetadatum> fields = assetMap.Value
            .Where(f => f.Key is MetadataText)
            .ToDictionary(f => ((MetadataText)f.Key).Value ?? string.Empty, f => f.Value);

        string name = fields.TryGetValue("name", out TransactionMetadatum? n) && n is MetadataText nt
            ? nt.Value ?? string.Empty : string.Empty;

        string imageUri = fields.TryGetValue("image", out TransactionMetadatum? img)
            ? img switch
            {
                MetadataText imageText => imageText.Value ?? string.Empty,
                MetadatumList imageList => string.Join(string.Empty, imageList.Value.OfType<MetadataText>().Select(t => t.Value)),
                _ => string.Empty
            }
            : string.Empty;

        string description = fields.TryGetValue("description", out TransactionMetadatum? d) && d is MetadataText dt
            ? dt.Value ?? string.Empty : string.Empty;

        return new TokenMetadataOnChain(
            Subject: $"{policyId}{assetNameHex}",
            PolicyId: policyId,
            AssetName: assetNameHex,
            Name: name,
            Logo: IsValidUri(imageUri) ? imageUri : string.Empty,
            Description: description,
            Quantity: quantity,
            Decimals: 0,
            TokenType: TokenType.CIP25
        );
    }

    private static bool IsValidUri(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        bool hasValidScheme = ValidSchemes.Any(scheme => uri.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));

        if (!hasValidScheme)
            return false;

        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return DataUriRegex.IsMatch(uri);
        }

        return uri.Length > ValidSchemes.First(s => uri.StartsWith(s, StringComparison.OrdinalIgnoreCase)).Length;
    }

    private static bool TokensAreEqual(TokenMetadataOnChain a, TokenMetadataOnChain b) =>
        a.Name == b.Name &&
        a.Logo == b.Logo &&
        a.Description == b.Description &&
        a.Quantity == b.Quantity &&
        a.Decimals == b.Decimals;

    [GeneratedRegex(@"^data:image\/[a-zA-Z0-9]+(?:\+[a-zA-Z0-9]+)?;base64,", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}