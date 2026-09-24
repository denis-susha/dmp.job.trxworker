using DMP.DataAccess.Models;
using DMP.DataAccess.Models.Enumerations;

namespace DMP.BL.Services;

public interface ISettingsService
{
    CryptocurrencySettingDAL GetCurrencySetting(Cryptocurrency cryptocurrencyId);
}
