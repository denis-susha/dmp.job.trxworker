using System.Text.Json.Serialization;

namespace DMP.BL.Models.Bitcart;

/// <summary>
/// The fields of a Bitcart invoice (GET /invoices/{id}) that the worker uses.
/// </summary>
public class InvoiceResponseBC
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("status")]
    public string Status { get; set; } = null!;

    [JsonPropertyName("price")]
    public string Price { get; set; } = null!;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = null!;

    [JsonPropertyName("paid_currency")]
    public string? PaidCurrency { get; set; }

    [JsonPropertyName("sent_amount")]
    public decimal? SentAmount { get; set; }

    [JsonPropertyName("paid_date")]
    public string? PaidDate { get; set; }

    [JsonPropertyName("payments")]
    public PaymentBC[] Payments { get; set; } = [];
}
