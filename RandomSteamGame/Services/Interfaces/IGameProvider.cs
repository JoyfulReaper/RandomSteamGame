using ErrorOr;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services.Interfaces;

public interface IGameProvider
{
    string ProviderKey { get; }
    Task<ErrorOr<OwnedGamesResponse>> GetOwnedGamesAsync(long userId);
    Task<ErrorOr<RandomGameResponse>> GetRandomGameAsync(long userId);
    Task<ErrorOr<GameDetails>> GetRandomGameDetailsAsync(long userId);
    Task<ErrorOr<long>> ResolveIdentifierAsync(string identifier);
}