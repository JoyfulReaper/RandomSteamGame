using System.Text.Json.Serialization;

namespace RandomSteamGameBlazor.Shared.Contracts;

public class SteamStoreApiContracts
{
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

        [JsonPropertyName("developers")]
        public List<string> Developers { get; set; } = new();

        [JsonPropertyName("publishers")]
        public List<string> publishers { get; set; } = new();

        public Platforms Platforms { get; set; } = new();

        [JsonPropertyName("categories")]
        public List<Category> Categories { get; set; } = new();

        [JsonPropertyName("genres")]
        public List<Genre> Genres { get; set; } = new();


        public Recommendations Recommendations { get; set; } = new();

        [JsonPropertyName("release_date")]
        public ReleaseDate ReleaseDate { get; set; } = default!;

        [JsonPropertyName("background")]
        public string? Background { get; set; } = default!;

        [JsonPropertyName("background_raw")]
        public string BackgroundRaw { get; set; } = default!;
    }

    public class PcRequirements
    {
        public string? Minimum { get; set; }
        public string? Recommended { get; set; }
    }

    public class MacRequirements
    {
        public string? Minimum { get; set; }
        public string? Recommended { get; set; }
    }

    public class LinuxRequirements
    {
        public string? Minimum { get; set; }
        public string? Recommended { get; set; }
    }

    public class Platforms
    {
        public bool Windows { get; set; }
        public bool Mac { get; set; }
        public bool Linux { get; set; }
    }

    public class Recommendations
    {
        public int Total { get; set; }
    }

    public class ReleaseDate
    {
        [JsonPropertyName("coming_soon")]
        public bool ComingSoon { get; set; }
        public string? Date { get; set; }
    }
    
    public class Category
    {
        public int Id { get; set; }
        public string? Description { get; set; }
    }

    public class Genre
    {
        public string? Id { get; set; }
        public string? Description { get; set; }
    }
}