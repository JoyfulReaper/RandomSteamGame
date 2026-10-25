using System.ComponentModel.DataAnnotations;

namespace RandomSteamGame.Options;

public sealed class FeatureVotingOptions
{
    public const string SectionName = "FeatureVoting";
    [Range(1, 100000)] public int BrowserPermitLimit { get; set; } = 20;
    [Range(1, 100000)] public int IpPermitLimit { get; set; } = 120;
    [Range(1, 100000)] public int GlobalPermitLimit { get; set; } = 600;
    [Range(1, 60)] public int GlobalConcurrency { get; set; } = 8;
    [Range(1, 3600)] public int WindowSeconds { get; set; } = 60;
}
