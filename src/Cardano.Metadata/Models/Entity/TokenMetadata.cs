namespace Cardano.Metadata.Models.Entity;
public record TokenMetadata(
    string Subject,
    string Name,
    string Ticker,
    string Policy,
    int Decimals,
    string? Url,
    string? Logo,
    string? Description
);