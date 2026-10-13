using JoyfulReaperLib.JRNet;

namespace RandomSteamGame.Services;

internal static class RandomSteamIngressNetwork
{
    private static readonly string[] PublicHosts = ["randomsteam.kgivler.com"];
    private static readonly string[] YggdrasilHostSuffixes = ["ygg.kgivler.com"];

    public static string FromHost(string? host) =>
        IngressNetworkClassifier.FromHost(
            host,
            publicHosts: PublicHosts,
            yggdrasilHostSuffixes: YggdrasilHostSuffixes);
}
