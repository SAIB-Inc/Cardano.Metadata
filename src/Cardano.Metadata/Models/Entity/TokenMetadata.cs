
namespace Cardano.Metadata.Models.Entity;

public record TokenMetadata
{
    public string Subject { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Policy { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Logo { get; set; } = string.Empty;
    public int Decimals { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
}