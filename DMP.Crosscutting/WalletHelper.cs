namespace DMP.Crosscutting;

public static class WalletHelper
{
    public static string TruncateAddress(string address, int prefixLength = 4, int suffixLength = 4)
    {
        if (string.IsNullOrEmpty(address) || address.Length <= prefixLength + suffixLength)
        {
            return address;
        }

        return $"{address[..prefixLength]}...{address[^suffixLength..]}";
    }
}
