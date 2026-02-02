namespace COMP.API.Configuration;

public class GatewayOptions
{
    public const string SectionName = "Gateway";

    public string? Ipfs { get; set; }
    public string? Arweave { get; set; }
}
