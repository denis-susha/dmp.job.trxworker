namespace DMP.Crosscutting;

public static class MathHelper
{
    /// <summary>
    /// Truncates <paramref name="d"/> towards zero to the given number of decimal places.
    /// </summary>
    public static decimal Truncate(decimal d, byte decimals)
    {
        var r = Math.Round(d, decimals);

        if (d > 0 && r > d)
        {
            return r - new decimal(1, 0, 0, false, decimals);
        }

        if (d < 0 && r < d)
        {
            return r + new decimal(1, 0, 0, false, decimals);
        }

        return r;
    }
}
