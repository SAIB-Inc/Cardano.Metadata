namespace Cardano.Metadata.Models.Entity;

public record MetaData(
    string Subject,
    string Name,
    string Ticker,
    string Policy,
    int Decimals,
    string? Url,
    string? Logo,
    string? Description
);
