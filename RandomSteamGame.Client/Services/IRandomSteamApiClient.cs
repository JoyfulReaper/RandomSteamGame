using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Client.Services;

public interface IRandomSteamApiClient
{
    Task<ApiResult<OwnedGamesResponse>> GetOwnedGamesAsync(string provider, long steamId, CancellationToken cancellationToken = default);
    Task<ApiResult<bool>> InvalidateOwnedGamesCacheAsync(string provider, long steamId, CancellationToken cancellationToken = default);
    Task<ApiResult<GameDetails>> GetRandomGameDetailsAsync(string provider, long? steamId = null,
        string? vanityUrl = null, bool unplayedOnly = false, CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? excludedGameIds = null);
    Task<ApiResult<long>> ResolveVanityUrlAsync(string provider, string vanityUrl, CancellationToken cancellationToken = default);
    Task<ApiResult<AppStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default);
}
