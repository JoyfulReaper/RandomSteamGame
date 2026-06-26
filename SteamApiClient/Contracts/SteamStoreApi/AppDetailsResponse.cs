/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using SteamApiClient.Converters;
using System.Text.Json.Serialization;

namespace SteamApiClient.Contracts.SteamStoreApi;

public record AppDetailsResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] AppData? AppData
);

public record AppData(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    [property: JsonPropertyName("steam_appid")] int SteamAppId,

    [property: JsonConverter(typeof(SteamObjectOrEmptyArrayConverter<string>))]
    [property: JsonPropertyName("required_age")] string? RequiredAge,

    [property: JsonPropertyName("is_free")] bool IsFree,
    [property: JsonPropertyName("dlc")] List<int> Dlc,
    [property: JsonPropertyName("detailed_description")] string DetailedDescription,
    [property: JsonPropertyName("about_the_game")] string AboutTheGame,
    [property: JsonPropertyName("short_description")] string ShortDescription,
    [property: JsonPropertyName("supported_languages")] string SupportedLanguages,
    [property: JsonPropertyName("header_image")] string HeaderImage,
    [property: JsonPropertyName("website")] string? Website,

    [property: JsonConverter(typeof(SteamObjectOrEmptyArrayConverter<PcRequirements>))]
    [property: JsonPropertyName("pc_requirements")] PcRequirements? PcRequirements,

    [property: JsonConverter(typeof(SteamObjectOrEmptyArrayConverter<MacRequirements>))]
    [property: JsonPropertyName("mac_requirements")] MacRequirements? MacRequirements,

    [property: JsonConverter(typeof(SteamObjectOrEmptyArrayConverter<LinuxRequirements>))]
    [property: JsonPropertyName("linux_requirements")] LinuxRequirements? LinuxRequirements,

    [property: JsonPropertyName("developers")] List<string> Developers,
    [property: JsonPropertyName("publishers")] List<string> Publishers,
    [property: JsonPropertyName("price_overview")] PriceOverview? PriceOverview,
    [property: JsonPropertyName("packages")] int[]? Packages,

    [property: JsonConverter(typeof(SteamObjectOrEmptyArrayConverter<List<PackageGroups>>))]
    [property: JsonPropertyName("package_groups")] List<PackageGroups>? PackageGroups,

    [property: JsonPropertyName("platforms")] Platforms? Platforms,
    [property: JsonPropertyName("metacritic")] Metacritic? Metacritic,
    [property: JsonPropertyName("categories")] List<Category> Categories,
    [property: JsonPropertyName("genres")] List<Genre> Genres,
    [property: JsonPropertyName("screenshots")] List<Screenshot> Screenshots,
    [property: JsonPropertyName("recommendations")] Recommendations? Recommendations,
    [property: JsonPropertyName("achievements")] Achievements? Achievements,
    [property: JsonPropertyName("release_date")] ReleaseDate ReleaseDate,
    [property: JsonPropertyName("support_info")] SupportInfo? SupportInfo,
    [property: JsonPropertyName("background")] string? Background,
    [property: JsonPropertyName("background_raw")] string BackgroundRaw,
    [property: JsonPropertyName("content_descriptors")] ContentDescriptors? ContentDescriptors
);

public record PcRequirements(string? Minimum, string? Recommended);
public record MacRequirements(string? Minimum, string? Recommended);
public record LinuxRequirements(string? Minimum, string? Recommended);

public record PriceOverview(
    string? Currency,
    int Initial,
    int Final,
    [property: JsonPropertyName("discount_percent")] int DiscountPercent,
    [property: JsonPropertyName("initial_formatted")] string? InitialFormatted,
    [property: JsonPropertyName("final_formatted")] string? FinalFormatted
);

public record Platforms(bool Windows, bool Mac, bool Linux);
public record Metacritic(int Score, string? Url);
public record Recommendations(int Total);

public record Achievements(
    int Total,
    Highlighted[]? Highlighted
);

public record Highlighted(string? Name, string? Path);

public record ReleaseDate(
    [property: JsonPropertyName("coming_soon")] bool ComingSoon,
    string? Date
);

public record SupportInfo(string? Url, string? Email);
public record ContentDescriptors(object[]? Ids, object? Notes);

public record PackageGroups(
    string? Name,
    string? Title,
    string? Description,
    [property: JsonPropertyName("selection_text")] string? SelectionText,
    [property: JsonPropertyName("save_text")] string? SaveText,
    [property: JsonPropertyName("display_type")] int DisplayType,
    [property: JsonPropertyName("is_recurring_subscription")] string? IsRecurringSubscription,
    Sub[]? Subs
);

public record Sub(
    int PackageId,
    [property: JsonPropertyName("percent_savings_text")] string? PercentSavingsText,
    [property: JsonPropertyName("percent_savings")] int PercentSavings,
    [property: JsonPropertyName("option_text")] string? OptionText,
    [property: JsonPropertyName("option_description")] string? OptionDescription,
    [property: JsonPropertyName("can_get_free_license")] string? CanGetFreeLicense,
    [property: JsonPropertyName("is_free_license")] bool IsFreeLicense,
    [property: JsonPropertyName("price_in_cents_with_discount")] int PriceInCentsWithDiscount
);

public record Category(int Id, string? Description);
public record Genre(string? Id, string? Description);

public record Screenshot(
    int Id,
    [property: JsonPropertyName("path_thumbnail")] string? PathThumbnail,
    [property: JsonPropertyName("path_full")] string? PathFull
);