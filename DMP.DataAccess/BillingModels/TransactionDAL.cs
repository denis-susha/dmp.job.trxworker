using DMP.DataAccess.BillingModels.Enumerations;
using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.BillingModels;

public class TransactionDAL
{
    // ULID
    public string TransactionId { get; set; } = null!;

    public int? OrderId { get; set; }

    public DateTimeOffset? TransactionDate { get; set; }

    public TransactionType TransactionType { get; set; }

    // Amount in Cryptocurrency.
    public decimal Amount { get; set; }

    public Cryptocurrency Cryptocurrency { get; set; }

    // The user involved in the transaction: sender or receiver, depending on the transaction type.
    public Guid UserId { get; set; }

    // Target account.
    public int AccountId { get; set; }

    public TransactionStatus Status { get; set; }

    // Bitcart payment method id.
    public string? PaymentMethodId { get; set; }

    // Bitcart invoice id (or payout / funds transfer id).
    public string? ReferenceNumber { get; set; }

    public string? Description { get; set; }

    // Seller id, if applicable.
    public Guid? SellerId { get; set; }

    // Fee charged for processing the transaction, if applicable.
    public decimal FeeAmount { get; set; }

    // Amount before the fee is subtracted.
    public decimal NetAmount { get; set; }

    // Where the transaction originated (web app, admin, ...).
    public TransactionSourceType TransactionSource { get; set; }

    public string? SourceTransactionId { get; set; }

    // Links to a prior transaction this one belongs to (e.g. the sale a fee was charged for).
    public string? RelatedTransactionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public string? IPAddress { get; set; }

    // Exchange rate between PriceCurrency and Cryptocurrency.
    public decimal? Rate { get; set; }

    public int? OrderLineId { get; set; }

    // Invoice price.
    public decimal Price { get; set; }

    // Invoice price currency.
    public Currency PriceCurrency { get; set; }
}
