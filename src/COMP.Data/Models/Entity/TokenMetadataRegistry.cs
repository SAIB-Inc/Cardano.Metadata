namespace COMP.Data.Models.Entity;

public record TokenMetadataRegistry(
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
