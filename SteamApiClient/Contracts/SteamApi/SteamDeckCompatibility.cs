using System.Text.Json.Serialization;

namespace SteamApiClient.Contracts.SteamApi;

public enum SteamDeckCompatibilityCategory
{
    Unknown = 0,
    Unsupported = 1,
    Playable = 2,
    Verified = 3
}

public sealed record StoreBrowseItemsResponse(
    [property: JsonPropertyName("response")]
    StoreBrowseResponse? Response);

public sealed record StoreBrowseResponse(
    [property: JsonPropertyName("store_items")]
    List<StoreBrowseItem>? StoreItems);

public sealed record StoreBrowseItem(
    [property: JsonPropertyName("appid")]
    int AppId,

    [property: JsonPropertyName("platforms")]
    StoreBrowsePlatforms? Platforms);

public sealed record StoreBrowsePlatforms(
    [property: JsonPropertyName("steam_deck_compat_category")]
    int SteamDeckCompatCategory);