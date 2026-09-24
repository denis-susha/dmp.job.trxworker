using System.Text.Json.Serialization;

namespace DMP.BL.Models.Bitcart;

/// <summary>
/// The fields of a Bitcart invoice payment method that the worker uses.
/// </summary>
public class PaymentBC
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = null!;

    [JsonPropertyName("rate")]
    public string Rate { get; set; } = null!;
}
