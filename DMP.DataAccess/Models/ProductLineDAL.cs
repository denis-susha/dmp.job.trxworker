namespace DMP.DataAccess.Models;

public class ProductLineDAL
{
    public int ProductLineId { get; set; }
    public int ProductId { get; set; }
    public string Value { get; set; } = null!;
    public bool IsSold { get; set; }

    public virtual ProductDAL? Product { get; set; }
}
