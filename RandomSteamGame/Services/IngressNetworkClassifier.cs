namespace RandomSteamGame.Services;

public static class IngressNetworkClassifier
{
    public const string Public = "public";
    public const string Dn42 = "dn42";
    public const string Yggdrasil = "yggdrasil";
    public const string Unknown = "unknown";

    public static string FromHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return Unknown;
        }

        host = host.Trim().TrimEnd('.');

        if (host.EndsWith(".dn42", StringComparison.OrdinalIgnoreCase))
        {
            return Dn42;
        }

        if (host.EndsWith(".ygg.kgivler.com", StringComparison.OrdinalIgnoreCase))
        {
            return Yggdrasil;
        }

        if (host.Equals(
                "randomsteam.kgivler.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return Public;
        }

        return Unknown;
    }
}