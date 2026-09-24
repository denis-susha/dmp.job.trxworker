using DMP.DataAccess.BillingModels.Enumerations;
using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.BillingModels;

public class AccountDAL
{
    public int AccountId { get; set; }
    public Guid UserId { get; set; }
    public AccountType AccountType { get; set; }
    public Cryptocurrency Cryptocurrency { get; set; }
    public decimal AccountBalance { get; set; }
    public decimal ReservedBalance { get; set; }
    public AccountStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
