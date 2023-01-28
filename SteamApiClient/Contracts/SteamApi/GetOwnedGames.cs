using System.Text.Json.Serialization;

namespace SteamApiClient.Contracts.SteamApi;

public class OwnedGamesResponse
{
    [JsonPropertyName("response")]
    public OwnedGames Response { get; set; } = default!;
}
public sealed class OwnedGames
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<Game> Games { get; set; } = new List<Game>();
}

public class Game
{
    [JsonPropertyName("appid")]
    public int AppId { get; set; }
    
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }

    [JsonPropertyName("img_icon_url")]
    public string? ImgIconUrl { get; set; }
    
    [JsonPropertyName("playtime_windows_forever")]
    public int PlaytimeWindowsForever { get; set; }

    [JsonPropertyName("playtime_mac_forever")]
    public int PlaytimeMacForever { get; set; }

    [JsonPropertyName("playtime_linux_forever")]
    public int PlaytimeLinuxForever { get; set; }

    [JsonPropertyName("rtime_last_played")]
    public long RTimeLastPlayed { get; set; }

    [JsonPropertyName("playtime_2weeks")]
    public int Playtime2Weeks { get; set; }
}
