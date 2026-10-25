using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Client.Models;

public sealed class FeatureVotingResult
{
    public int Status { get; set; }
    public FeatureProposalResponse[]? Proposals { get; set; }
    public int? RetryAfterSeconds { get; set; }
}
