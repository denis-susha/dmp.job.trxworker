using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.Models.Sale;

public class SalesLineDAL
{
    public int SalesLineId { get; set; }
    public int SalesHeaderId { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }
    public Currency Currency { get; set; }
    public int Quantity { get; set; }
    public string TransactionId { get; set; } = null!;

    public virtual SalesHeaderDAL SalesHeader { get; set; } = null!;
    public virtual ProductDAL Product { get; set; } = null!;
}
