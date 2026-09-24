using DMP.DataAccess;
using DMP.DataAccess.Models;
using DMP.DataAccess.Models.Enumerations;
using Microsoft.EntityFrameworkCore;

namespace DMP.BL.Services;

/// <summary>
/// Cryptocurrency settings loaded once at startup (registered as a singleton).
/// </summary>
public class SettingsService(IDbContextFactory<DmpDbContext> dmpContextFactory) : ISettingsService
{
    private readonly List<CryptocurrencySettingDAL> _cryptocurrencySettings = LoadSettings(dmpContextFactory);

    public CryptocurrencySettingDAL GetCurrencySetting(Cryptocurrency cryptocurrencyId) =>
        _cryptocurrencySettings.First(c => c.CryptocurrencyId == cryptocurrencyId);

    private static List<CryptocurrencySettingDAL> LoadSettings(IDbContextFactory<DmpDbContext> contextFactory)
    {
        using var context = contextFactory.CreateDbContext();
        return context.CryptocurrencySettings.AsNoTracking().ToList();
    }
}
