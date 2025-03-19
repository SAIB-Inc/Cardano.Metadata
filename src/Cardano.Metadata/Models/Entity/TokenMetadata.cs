
namespace Cardano.Metadata.Models.Entity;

public record TokenMetadata
{
    public string? Subject { get; init; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Policy { get; set; }
    public string? Ticker { get; set; }
    public string? Url { get; set; }
    public string? Logo { get; set; }
    public int? Decimals { get; set; }
}