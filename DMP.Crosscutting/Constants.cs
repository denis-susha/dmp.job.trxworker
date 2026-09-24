namespace DMP.Crosscutting;

public static class Constants
{
    public const string LogoPath = "static/logo_color.png";

    public static readonly IReadOnlyDictionary<string, string> DmpEmails = new Dictionary<string, string>
    {
        ["noreply"] = "noreply@filezon.com",
        ["store"] = "store@filezon.com",
    };
}
