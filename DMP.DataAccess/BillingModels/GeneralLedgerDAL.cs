using DMP.DataAccess.BillingModels.Enumerations;
using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.BillingModels;

public class GeneralLedgerDAL
{
    public int GeneralLedgerId { get; set; }

    public string TransactionId { get; set; } = null!;

    // The date the transaction occurred.
    public DateTimeOffset Date { get; set; }

    public int AccountId { get; set; }

    public string? Description { get; set; }

    // The amount debited from the source account.
    public decimal DebitAmount { get; set; }

    // The amount credited to the target account.
    public decimal CreditAmount { get; set; }

    public TransactionType TransactionType { get; set; }

    public Cryptocurrency Cryptocurrency { get; set; }

    public TransactionSourceType TransactionSource { get; set; }

    // The user or system process that created the record.
    public Guid? CreatedBy { get; set; }

    public string? ReferenceNumber { get; set; }

    public decimal? Rate { get; set; }
}
