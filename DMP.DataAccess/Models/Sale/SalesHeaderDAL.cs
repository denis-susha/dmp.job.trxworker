using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.Models.Sale;

public class SalesHeaderDAL
{
    public int SalesHeaderId { get; set; }
    public Guid SellerId { get; set; }
    public decimal Amount { get; set; }
    public Cryptocurrency Cryptocurrency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int OrderId { get; set; }

    public virtual ICollection<SalesLineDAL>? SalesLines { get; set; }
}
