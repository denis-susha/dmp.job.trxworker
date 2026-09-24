using DMP.DataAccess.Models.Enumerations;

namespace DMP.BL.Models;

/// <summary>
/// Well-known system account ids in the billing database, per cryptocurrency.
/// </summary>
public static class SystemAccount
{
    // Receives incoming client payments and incoming transfers.
    public static readonly IReadOnlyDictionary<Cryptocurrency, int> Credit = new Dictionary<Cryptocurrency, int>
    {
        [Cryptocurrency.BTC] = 1001,
        [Cryptocurrency.TRX] = 1002,
        [Cryptocurrency.USDT_TRC20] = 1003,
        [Cryptocurrency.BCH] = 1004,
        [Cryptocurrency.BNB] = 1005,
        [Cryptocurrency.USDT_BEP20] = 1006,
        [Cryptocurrency.ETH] = 1007,
        [Cryptocurrency.USDT_ERC20] = 1008,
        [Cryptocurrency.POL] = 1009,
        [Cryptocurrency.USDT_MATIC] = 1010,
        [Cryptocurrency.XMR] = 1011,
        [Cryptocurrency.LTC] = 1012,
        [Cryptocurrency.USDC_ERC20] = 1013,
    };

    // Source of funds moved to sellers (sales, bonuses).
    public static readonly IReadOnlyDictionary<Cryptocurrency, int> Debit = new Dictionary<Cryptocurrency, int>
    {
        [Cryptocurrency.BTC] = 2001,
        [Cryptocurrency.TRX] = 2002,
        [Cryptocurrency.USDT_TRC20] = 2003,
        [Cryptocurrency.BCH] = 2004,
        [Cryptocurrency.BNB] = 2005,
        [Cryptocurrency.USDT_BEP20] = 2006,
        [Cryptocurrency.ETH] = 2007,
        [Cryptocurrency.USDT_ERC20] = 2008,
        [Cryptocurrency.POL] = 2009,
        [Cryptocurrency.USDT_MATIC] = 2010,
        [Cryptocurrency.XMR] = 2011,
        [Cryptocurrency.LTC] = 2012,
        [Cryptocurrency.USDC_ERC20] = 2013,
    };

    // Receives funds withdrawn by sellers.
    public static readonly IReadOnlyDictionary<Cryptocurrency, int> Payout = new Dictionary<Cryptocurrency, int>
    {
        [Cryptocurrency.BTC] = 3001,
        [Cryptocurrency.TRX] = 3002,
        [Cryptocurrency.USDT_TRC20] = 3003,
        [Cryptocurrency.BCH] = 3004,
        [Cryptocurrency.BNB] = 3005,
        [Cryptocurrency.USDT_BEP20] = 3006,
        [Cryptocurrency.ETH] = 3007,
        [Cryptocurrency.USDT_ERC20] = 3008,
        [Cryptocurrency.POL] = 3009,
        [Cryptocurrency.USDT_MATIC] = 3010,
        [Cryptocurrency.XMR] = 3011,
        [Cryptocurrency.LTC] = 3012,
        [Cryptocurrency.USDC_ERC20] = 3013,
    };

    // Collects marketplace fees.
    public static readonly IReadOnlyDictionary<Cryptocurrency, int> Fees = new Dictionary<Cryptocurrency, int>
    {
        [Cryptocurrency.BTC] = 5001,
        [Cryptocurrency.TRX] = 5002,
        [Cryptocurrency.USDT_TRC20] = 5003,
        [Cryptocurrency.BCH] = 5004,
        [Cryptocurrency.BNB] = 5005,
        [Cryptocurrency.USDT_BEP20] = 5006,
        [Cryptocurrency.ETH] = 5007,
        [Cryptocurrency.USDT_ERC20] = 5008,
        [Cryptocurrency.POL] = 5009,
        [Cryptocurrency.USDT_MATIC] = 5010,
        [Cryptocurrency.XMR] = 5011,
        [Cryptocurrency.LTC] = 5012,
        [Cryptocurrency.USDC_ERC20] = 5013,
    };
}
