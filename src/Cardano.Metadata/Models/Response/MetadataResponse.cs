

namespace Cardano.Metadata.Models.Response;

public record MetadataResponse
{
        public required string Subject { get; init; }
        public required ValueResponse<int> Decimals { get; set; }
        public ValueResponse<string>? Description { get; set; }
        public required ValueResponse<string> Name { get; set; }
        public required ValueResponse<string> Ticker { get; set; }
        public required ValueResponse<string> Url { get; set; }
        public required ValueResponse<string> Logo { get; set; }
}