
namespace Cardano.Metadata.Models.Response;

public record ValueResponse<T>
{
    public required T Value { get; set; }
}