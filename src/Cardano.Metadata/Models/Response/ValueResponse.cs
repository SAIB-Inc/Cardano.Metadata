namespace Cardano.Metadata.Models.Response;

public record ValueResponse<T>
{
    public T? Value { get; set; }
}