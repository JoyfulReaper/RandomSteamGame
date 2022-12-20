
using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamStoreApiContracts;

public class AppDetails
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("steam_appid")]
    public int SteamAppid { get; set; }

    // Sometime the API sends a string other times it sends an int...
    //[JsonPropertyName("required_age")]
    //public int RequiredAge { get; set; }

    [JsonPropertyName("is_free")]
    public bool IsFree { get; set; }

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

    [JsonPropertyName("developers")]
    public List<string> Developers { get; set; } = new();

    [JsonPropertyName("publishers")]
    public List<string> publishers { get; set; } = new();

    [JsonPropertyName("categories")]
    public List<Category> Categories { get; set; } = new();

    [JsonPropertyName("genres")]
    public List<Genre> Genres { get; set; } = new();

    [JsonPropertyName("screenshots")]
    public List<Screenshot> screenshots { get; set; } = new();

    [JsonPropertyName("release_date")]
    public ReleaseDate ReleaseDate { get; set; } = default!;

    [JsonPropertyName("background")]
    public string Background { get; set; } = default!;

    [JsonPropertyName("background_raw")]
    public string BackgroundRaw { get; set; } = default!;
}
