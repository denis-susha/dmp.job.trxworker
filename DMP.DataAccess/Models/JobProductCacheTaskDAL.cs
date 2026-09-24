namespace DMP.DataAccess.Models;

public class JobProductCacheTaskDAL
{
    public long JobProductCacheTaskId { get; set; }
    public int ProductId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
