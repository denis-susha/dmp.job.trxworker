using DMP.DataAccess.Models.Enumerations;
using DMP.DataAccess.Models.Sale;

namespace DMP.DataAccess.Models;

public class ProductDAL
{
    public int ProductId { get; set; }
    public int MenuCategoryId { get; set; }
    public UserFeatureDAL? UserFeature { get; set; }
    public string Slug { get; set; } = null!;
    public Guid SellerId { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public bool Unlimited { get; set; }
    public string[]? ImgLinks { get; set; }
    public ProductStatus Status { get; set; }
    public bool IsLines { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public virtual ICollection<ProductLineDAL>? ProductLines { get; set; }
    public virtual ICollection<SalesLineDAL>? SalesLines { get; set; }
}
