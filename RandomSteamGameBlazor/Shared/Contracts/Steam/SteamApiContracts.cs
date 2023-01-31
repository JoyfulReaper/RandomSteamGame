using System.Text.Json.Serialization;

namespace RandomSteamGameBlazor.Shared.Contracts.Steam;

public class AppDetailsResponse
{
    [JsonPropertyName("success")]

    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public AppData? AppData { get; set; }
}

public class AppData
{

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("steam_appid")]
    public int SteamAppid { get; set; }

    [JsonPropertyName("detailed_description")]
    public string DetailedDescription { get; set; } = default!;

    [JsonPropertyName("about_the_game")]
    public string AboutTheGame { get; set; } = default!;

    [JsonPropertyName("short_description")]
    public string ShortDescription { get; set; } = default!;

    [JsonPropertyName("header_image")]
    public string HeaderImage { get; set; } = default!;

    [JsonPropertyName("website")]
    public string? Website { get; set; }

    [JsonPropertyName("developers")]
    public List<string> Developers { get; set; } = new();

    [JsonPropertyName("publishers")]
    public List<string> publishers { get; set; } = new();


    [JsonPropertyName("background")]
    public string? Background { get; set; } = default!;

    [JsonPropertyName("background_raw")]
    public string BackgroundRaw { get; set; } = default!;
}
