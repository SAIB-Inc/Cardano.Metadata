namespace Cardano.Metadata.Models.Entity;

public record MetaData(
    string Subject,
    string Name,
    string Ticker,
    string PolicyId,
    int Decimals,
    string? Policy,
    string? Url,
    string? Logo,
    string? Description
);
