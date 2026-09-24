namespace DMP.BL.Models.Bitcart;

// Parsed case-insensitively from the Bitcart invoice "status" field.
public enum InvoiceStatusBC
{
    Pending,
    Paid,
    Unconfirmed, // Electrum status, equivalent to Paid
    Confirmed,
    Expired,
    Invalid,
    Complete,
    Refunded,
}
