using Argus.Sync.Reducers;
using Chrysalis.Cbor.Extensions;
using Chrysalis.Cbor.Extensions.Cardano.Core;
using Chrysalis.Cbor.Extensions.Cardano.Core.Common;
using Chrysalis.Cbor.Extensions.Cardano.Core.Transaction;
using Chrysalis.Cbor.Extensions.Cardano.Core.TransactionWitness;
using Chrysalis.Cbor.Serialization;
using Chrysalis.Cbor.Types.Cardano.Core;
using Chrysalis.Cbor.Types.Cardano.Core.Common;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Cbor.Types.Cardano.Core.TransactionWitness;
using Chrysalis.Cbor.Types.Plutus;
using Chrysalis.Wallet.Utils;
using COMP.Data.Data;
using COMP.Data.Models.Entity;
using Microsoft.EntityFrameworkCore;

namespace Comp.Sync.Reducers;

public class CIP68Reducer(
    IDbContextFactory<MetadataDbContext> dbContextFactory,
    ILogger<CIP68Reducer> logger
) : IReducer<TokenMetadataOnChain>
{
    private const string REFERENCE_PREFIX = "000643b0";
    private static readonly Dictionary<string, string> UserTokenPrefixes = new()
    {
        { "FT", "000de140" },
        { "NFT", "0014df10" },
        { "RFT", "001bc280" }
    };

    // Rollback is intentionally not implemented - this metadata indexer is append-only
    // and doesn't track historical state. On-chain metadata updates are rare and
    // rollbacks would require re-syncing from scratch.
    public async Task RollBackwardAsync(ulong slot) => await Task.CompletedTask;

    public async Task RollForwardAsync(Block block)
    {
        List<TransactionBody> txBodies = [.. block.TransactionBodies()];
        List<TransactionWitnessSet> witnessSets = [.. block.TransactionWitnessSets()];

        Dictionary<string, (string policyId, string baseName, byte[] datumBytes)> referenceTokens = ExtractAllReferenceTokens(txBodies, witnessSets, logger);
        if (referenceTokens.Count == 0)
            return;

        Dictionary<string, TokenMetadataOnChain> tokensToProcess = await ProcessReferenceTokensAsync(referenceTokens, txBodies);
        if (tokensToProcess.Count > 0)
            await SaveTokensAsync(tokensToProcess);
    }

    private static Dictionary<string, (string policyId, string baseName, byte[] datumBytes)> ExtractAllReferenceTokens(
        List<TransactionBody> txBodies,
        List<TransactionWitnessSet> witnessSets,
        ILogger<CIP68Reducer> logger) =>
        txBodies
            .Select((tx, i) => (outputs: tx.Outputs()?.ToList(), witnessSet: i < witnessSets.Count ? witnessSets[i] : null))
            .Where(x => x.outputs is { Count: > 0 })
            .SelectMany(x => x.outputs!.SelectMany(output =>
            {
                Dictionary<byte[], TokenBundleOutput>? multiAsset = output.Amount()?.MultiAsset();
                if (multiAsset is null) return [];

                return multiAsset.SelectMany(policy =>
                {
                    string policyId = Convert.ToHexString(policy.Key).ToLowerInvariant();
                    return policy.Value.Value
                        .Where(asset => asset.Value > 0)
                        .Select(asset => Convert.ToHexString(asset.Key).ToLowerInvariant())
                        .Where(assetNameHex => assetNameHex.StartsWith(REFERENCE_PREFIX))
                        .Select(assetNameHex =>
                        {
                            byte[]? datumBytes = ExtractDatum(output, x.witnessSet);
                            if (datumBytes is null)
                            {
                                logger.LogDebug("No datum found for reference token {Subject}", $"{policyId}{assetNameHex}");
                                return ((string policyId, string baseName, byte[] datumBytes)?)null;
                            }
                            return (policyId, baseName: assetNameHex[8..], datumBytes);
                        })
                        .Where(r => r is not null)
                        .Select(r => r!.Value);
                });
            }))
            .GroupBy(r => $"{r.policyId}{REFERENCE_PREFIX}{r.baseName}")
            .ToDictionary(g => g.Key, g => g.First());

    private async Task<Dictionary<string, TokenMetadataOnChain>> ProcessReferenceTokensAsync(
        Dictionary<string, (string policyId, string baseName, byte[] datumBytes)> referenceTokens,
        List<TransactionBody> txBodies)
    {
        Dictionary<string, long> mintedUserTokens = ExtractMintedUserTokens(txBodies);

        // Build all potential user token subjects for single batch DB query
        List<string> allPotentialSubjects = referenceTokens.Values
            .SelectMany(r => UserTokenPrefixes.Values.Select(prefix => $"{r.policyId}{prefix}{r.baseName}"))
            .ToList();

        // Single DB query to get all existing tokens
        await using MetadataDbContext db = await dbContextFactory.CreateDbContextAsync();
        Dictionary<string, TokenMetadataOnChain> existingTokens = await db.TokenMetadataOnChain
            .Where(t => allPotentialSubjects.Contains(t.Subject))
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Subject);

        return referenceTokens.Values
            .Select(r =>
            {
                // Determine user token subject in prefix priority order
                string? userTokenSubject = UserTokenPrefixes.Values
                    .Select(prefix => $"{r.policyId}{prefix}{r.baseName}")
                    .FirstOrDefault(s => mintedUserTokens.ContainsKey(s) || existingTokens.ContainsKey(s));

                if (userTokenSubject == null)
                    return null;

                // Get quantity from minted or existing
                long quantity = mintedUserTokens.GetValueOrDefault(userTokenSubject, 0);
                if (quantity == 0)
                    quantity = existingTokens.GetValueOrDefault(userTokenSubject)?.Quantity ?? 0;

                (string name, string image, string description, int? decimals) = ExtractCIP68Metadata(r.datumBytes);

                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(image))
                {
                    logger.LogWarning("Skipping {Subject} - CIP-68 requires name and image fields", userTokenSubject);
                    return null;
                }

                logger.LogDebug("Processed CIP-68 token: {Subject} (qty: {Quantity})", userTokenSubject, quantity);

                return new TokenMetadataOnChain(
                    Subject: userTokenSubject,
                    PolicyId: r.policyId,
                    AssetName: userTokenSubject[r.policyId.Length..],
                    Name: name,
                    Logo: image,
                    Description: description,
                    Quantity: quantity,
                    Decimals: decimals ?? 0,
                    TokenType: TokenType.CIP68
                );
            })
            .Where(t => t is not null)
            .ToDictionary(t => t!.Subject, t => t!);
    }

    private static Dictionary<string, long> ExtractMintedUserTokens(List<TransactionBody> txBodies) =>
        txBodies
            .Select(tx => tx.Mint())
            .Where(mint => mint is not null)
            .SelectMany(mint => mint!)
            .SelectMany(policyEntry =>
            {
                string policyId = Convert.ToHexString(policyEntry.Key).ToLowerInvariant();
                return policyEntry.Value.Value
                    .Where(asset => asset.Value > 0)
                    .Select(asset => (
                        subject: $"{policyId}{Convert.ToHexString(asset.Key).ToLowerInvariant()}",
                        quantity: asset.Value
                    ))
                    .Where(x => UserTokenPrefixes.Values.Any(x.subject[(policyId.Length)..].StartsWith));
            })
            .ToDictionary(x => x.subject, x => x.quantity);

    private static (string name, string image, string description, int? decimals) ExtractCIP68Metadata(byte[] datumBytes)
    {
        try
        {
            Cip68<PlutusData> datum = CborSerializer.Deserialize<Cip68<PlutusData>>(datumBytes);
            if (datum?.Metadata is not PlutusMap map)
                return (string.Empty, string.Empty, string.Empty, null);

            Dictionary<string, PlutusData> fields = map.PlutusData
                .Where(kvp => kvp.Key is PlutusBoundedBytes)
                .ToDictionary(
                    kvp => System.Text.Encoding.UTF8.GetString(((PlutusBoundedBytes)kvp.Key).Value),
                    kvp => kvp.Value);

            string name = fields.TryGetValue("name", out PlutusData? n) && n is PlutusBoundedBytes nb
                ? System.Text.Encoding.UTF8.GetString(nb.Value) : string.Empty;

            string image = fields.TryGetValue("image", out PlutusData? i) && i is PlutusBoundedBytes ib
                ? System.Text.Encoding.UTF8.GetString(ib.Value) : string.Empty;

            string description = fields.TryGetValue("description", out PlutusData? d) && d is PlutusBoundedBytes db
                ? System.Text.Encoding.UTF8.GetString(db.Value) : string.Empty;

            int? decimals = fields.TryGetValue("decimals", out PlutusData? dec)
                ? dec switch
                {
                    PlutusInt64 decInt => (int)decInt.Value,
                    PlutusUint64 decUint => (int)decUint.Value,
                    _ => null
                }
                : null;

            return (name, image, description, decimals);
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty, null);
        }
    }

    private static byte[]? ExtractDatum(TransactionOutput output, TransactionWitnessSet? witnessSet)
    {
        DatumOption? datumOption = output.DatumOption();

        if (datumOption is InlineDatumOption inlineDatum)
        {
            byte[] rawBytes = inlineDatum.Data.Value;
            if (rawBytes.Length >= 2 && rawBytes[0] == 0xD8 && rawBytes[1] == 0x18)
            {
                return inlineDatum.Data.GetValue();
            }
            return rawBytes;
        }

        if (datumOption != null)
            return null;

        byte[]? datumHash = output.DatumHash();
        if (datumHash == null || datumHash.Length == 0 || witnessSet == null)
            return null;

        IEnumerable<PlutusData> plutusDataSet = witnessSet.PlutusDataSet() ?? [];
        return ResolveDatumFromWitnessSet(datumHash, plutusDataSet);
    }

    private static byte[]? ResolveDatumFromWitnessSet(byte[] datumHash, IEnumerable<PlutusData> plutusDataSet) =>
        plutusDataSet
            .Select(pd => pd.Raw())
            .FirstOrDefault(datumBytes => HashUtil.Blake2b256(datumBytes).SequenceEqual(datumHash));

    private async Task SaveTokensAsync(Dictionary<string, TokenMetadataOnChain> tokensToProcess)
    {
        await using MetadataDbContext db = await dbContextFactory.CreateDbContextAsync();

        Dictionary<string, TokenMetadataOnChain> existingTokens = await db.TokenMetadataOnChain
            .Where(t => tokensToProcess.Keys.Contains(t.Subject))
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Subject);

        List<TokenMetadataOnChain> toInsert = tokensToProcess.Values
            .Where(t => !existingTokens.ContainsKey(t.Subject))
            .ToList();

        List<TokenMetadataOnChain> toUpdate = tokensToProcess.Values
            .Where(t => existingTokens.TryGetValue(t.Subject, out TokenMetadataOnChain? existing) && !TokensAreEqual(t, existing))
            .ToList();

        db.TokenMetadataOnChain.AddRange(toInsert);
        db.TokenMetadataOnChain.UpdateRange(toUpdate);

        toInsert.ForEach(t => logger.LogInformation("Inserted CIP-68 token {Subject} with quantity {Quantity}", t.Subject, t.Quantity));
        toUpdate.ForEach(t => logger.LogInformation("Updated CIP-68 token {Subject} - quantity: {OldQty} -> {NewQty}",
            t.Subject, existingTokens[t.Subject].Quantity, t.Quantity));

        await db.SaveChangesAsync();
    }

    private static bool TokensAreEqual(TokenMetadataOnChain a, TokenMetadataOnChain b) =>
        a.Name == b.Name &&
        a.Logo == b.Logo &&
        a.Description == b.Description &&
        a.Quantity == b.Quantity &&
        a.Decimals == b.Decimals;
}