using DMP.DataAccess.Models.Enumerations;

namespace DMP.DataAccess.Models;

public class CryptocurrencySettingDAL
{
    public Cryptocurrency CryptocurrencyId { get; set; }
    public string Code { get; set; } = null!;
    public string PlatformCode { get; set; } = null!;
    public string PlatformName { get; set; } = null!;
    public string TokenCode { get; set; } = null!;
    public string TokenName { get; set; } = null!;
    public string? Contract { get; set; }
    public string? Standart { get; set; }
    public byte Divisibility { get; set; }
}
