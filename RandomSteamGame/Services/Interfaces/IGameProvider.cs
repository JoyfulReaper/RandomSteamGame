using ErrorOr;
using RandomSteamGame.Services;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public interface IGameProvider
{
    string ProviderKey { get; }
    Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId, CancellationToken ct = default);
    Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false,
        CancellationToken ct = default);
    Task<RandomGamePickAttempt> GetRandomGamePickAsync(long userId, bool unplayedOnly = false,
        IReadOnlyCollection<int>? excludedGameIds = null, CancellationToken ct = default);
    Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier, CancellationToken ct = default);
    Task InvalidateOwnedGamesCacheAsync(long userId, CancellationToken ct = default);
}
