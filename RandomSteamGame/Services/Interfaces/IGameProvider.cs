using ErrorOr;
using RandomSteamGame.Services;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public interface IGameProvider
{
    string ProviderKey { get; }
    Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId);
    Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId, bool unplayedOnly = false);
    Task<ErrorOr<RandomGamePickResult>> GetRandomGamePickAsync(long userId, bool unplayedOnly = false);
    Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier);
    Task InvalidateOwnedGamesCacheAsync(long userId);
}
