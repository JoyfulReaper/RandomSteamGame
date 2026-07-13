namespace SteamApiClient.Services;

internal sealed record CachedValue<T>(
    T Value,
    DateTimeOffset CachedAt);
