using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public enum FeatureVoteResult { Saved, NotFound, Closed }

public interface IFeatureVotingService
{
    Task<IReadOnlyList<FeatureProposalResponse>> GetAsync(string browserHash, CancellationToken ct);
    Task<FeatureVoteResult> SetSupportedAsync(long featureId, string browserHash, bool supported, CancellationToken ct);
}
