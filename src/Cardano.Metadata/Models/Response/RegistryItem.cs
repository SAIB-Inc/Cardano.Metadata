namespace Cardano.Metadata.Models.Response;

public record RegistryItem
{
        public string? Subject { get; init; }
        public string? Policy { get; set; }
        public ValueResponse<int>? Decimals { get; set; }
        public ValueResponse<string>? Description { get; set; }
        public ValueResponse<string>? Name { get; set; }
        public ValueResponse<string>? Ticker { get; set; }
        public ValueResponse<string>? Url { get; set; }
        public ValueResponse<string>? Logo { get; set; }
}