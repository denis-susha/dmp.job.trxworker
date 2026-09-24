namespace DMP.DataAccess.BillingModels.Enumerations;

public enum TransactionType
{
    // Payment received from a buyer.
    Payment = 1,

    // Cancellation of a previously received payment with the money returned to the buyer.
    Refund = 2,

    // Funds moved to another account.
    Transfer = 3,

    // Manual adjustment.
    Adjustment = 4,

    // Funds withdrawn to the seller's external wallet.
    Payout = 5,

    // Subscription payment.
    RecurringPayment = 6,

    Fee = 7,

    // Share of an incoming payment moved to the seller's account.
    MovePayment = 8,

    IncomingTransfer = 9,
}
