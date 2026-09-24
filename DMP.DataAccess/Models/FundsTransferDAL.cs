using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.Models;

public class FundsTransferDAL
{
    public int FundsTransferId { get; set; }
    // Admin who initiated the transfer.
    public Guid UserId { get; set; }
    public Guid TargetUserId { get; set; }
    public decimal Amount { get; set; }
    public Cryptocurrency Cryptocurrency { get; set; }
    public string? Comment { get; set; }
    public FundsTransferStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
