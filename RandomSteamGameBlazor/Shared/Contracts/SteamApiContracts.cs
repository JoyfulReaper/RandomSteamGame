using System.Text.Json.Serialization;

namespace RandomSteamGameBlazor.Shared.Contracts.SteamApiContracts;

public class AppDetailsResponse
{
    [JsonPropertyName("success")]

    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public AppData? AppData { get; set; }
}

public class AppData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("steam_appid")]
    public int SteamAppid { get; set; }

    //TODO:Figure out how to deal with this
    //[JsonPropertyName("required_age")]
    //public int RequiredAge { get; set; }

    [JsonPropertyName("is_free")]
    public bool IsFree { get; set; }

    [JsonPropertyName("dlc")]
    public List<int> Dlc { get; set; } = new();

    [JsonPropertyName("detailed_description")]
    public string DetailedDescription { get; set; } = default!;

    [JsonPropertyName("about_the_game")]
    public string AboutTheGame { get; set; } = default!;

    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = default!;

    [JsonPropertyName("supported_languages")]
    public string SupportedLanguages { get; set; } = default!;

    [JsonPropertyName("header_image")]
    public string HeaderImage { get; set; } = default!;

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    //TODO: Custom deserializer since the Steam API is so shitty
    //[JsonPropertyName("pc_requirements")]
    //public PcRequirements PcRequirements { get; set; }

    //[JsonPropertyName("mac_requirements")]
    //public MacRequirements MacRequirements { get; set; }

    //[JsonPropertyName("linux_requirements")]
    //public LinuxRequirements LinuxRequirements { get; set; }

    [JsonPropertyName("developers")]
    public List<string> Developers { get; set; } = new();

    [JsonPropertyName("publishers")]
    public List<string> publishers { get; set; } = new();

    [JsonPropertyName("price_overview")]
    public PriceOverview PriceOverview { get; set; }

    public int[] Packages { get; set; }

    //[JsonPropertyName("package_groups")]
    //public PackageGroups[] PackageGroups { get; set; }

    public Platforms Platforms { get; set; }

    public Metacritic Metacritic { get; set; }

    [JsonPropertyName("categories")]
    public List<Category> Categories { get; set; } = new();

    [JsonPropertyName("genres")]
    public List<Genre> Genres { get; set; } = new();

    [JsonPropertyName("screenshots")]
    public List<Screenshot> screenshots { get; set; } = new();

    public Recommendations Recommendations { get; set; }

    public Achievements Achievements { get; set; }

    [JsonPropertyName("release_date")]
    public ReleaseDate ReleaseDate { get; set; } = default!;

    [JsonPropertyName("support_info")]
    public SupportInfo SupportInfo { get; set; }

    [JsonPropertyName("background")]
    public string? Background { get; set; } = default!;

    [JsonPropertyName("background_raw")]
    public string BackgroundRaw { get; set; } = default!;

    [JsonPropertyName("content_descriptors")]
    public ContentDescriptors ContentDescriptors { get; set; }
}

public class PcRequirements
{
    public string Minimum { get; set; }
    public string Recommended { get; set; }
}

public class MacRequirements
{
    public string Minimum { get; set; }
    public string Recommended { get; set; }
}

public class LinuxRequirements
{
    public string Minimum { get; set; }
    public string Recommended { get; set; }
}

public class PriceOverview
{
    public string Currency { get; set; }
    public int Initial { get; set; }
    public int Final { get; set; }

    [JsonPropertyName("discount_percent")]
    public int DiscountPercent { get; set; }

    [JsonPropertyName("initial_formatted")]
    public string InitialFormatted { get; set; }

    [JsonPropertyName("final_formatted")]
    public string FinalFormatted { get; set; }
}

public class Platforms
{
    public bool Windows { get; set; }
    public bool Mac { get; set; }
    public bool Linux { get; set; }
}

public class Metacritic
{
    public int Score { get; set; }
    public string Url { get; set; }
}

public class Recommendations
{
    public int Total { get; set; }
}

public class Achievements
{
    public int Total { get; set; }
    public Highlighted[] Highlighted { get; set; }
}

public class Highlighted
{
    public string Name { get; set; }
    public string Path { get; set; }
}

public class ReleaseDate
{
    [JsonPropertyName("coming_soon")]
    public bool ComingSoon { get; set; }
    public string Date { get; set; }
}

public class SupportInfo
{
    public string Url { get; set; }
    public string Email { get; set; }
}

public class ContentDescriptors
{
    public object[] Ids { get; set; }
    public object Notes { get; set; }
}

public class PackageGroups
{
    public string Name { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    [JsonPropertyName("selection_text")]
    public string SelectionText { get; set; }

    [JsonPropertyName("save_text")]
    public string SaveText { get; set; }

    [JsonPropertyName("display_type")]
    public int DisplayType { get; set; }

    [JsonPropertyName("is_recurring_subscription")]
    public string IsRecurringSubscription { get; set; }
    public Sub[] Subs { get; set; }
}

public class Sub
{
    public int PackageId { get; set; }
    [JsonPropertyName("percent_savings_text")]
    public string PercentSavingsText { get; set; }

    [JsonPropertyName("percent_savings")]
    public int PercentSavings { get; set; }

    [JsonPropertyName("option_text")]
    public string OptionText { get; set; }

    [JsonPropertyName("option_description")]
    public string OptionDescription { get; set; }

    [JsonPropertyName("can_get_free_license")]
    public string CanGetFreeLicense { get; set; }

    [JsonPropertyName("is_free_license")]
    public bool IsFreeLicense { get; set; }

    [JsonPropertyName("price_in_cents_with_discount")]
    public int PriceInCentsWithDiscount { get; set; }
}

public class Category
{
    public int Id { get; set; }
    public string Description { get; set; }
}

public class Genre
{
    public string Id { get; set; }
    public string Description { get; set; }
}

public class Screenshot
{
    public int Id { get; set; }

    [JsonPropertyName("path_thumbnail")]
    public string PathThumbnail { get; set; }

    [JsonPropertyName("path_full")]
    public string PathFull { get; set; }
}