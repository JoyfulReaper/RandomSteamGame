using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamApiContracts;

public sealed class Game
{
    [JsonPropertyName("appId")]
    public int AppId { get; set; }

    [JsonPropertyName("playtime_forever")]
    public int PlaytimeForever { get; set; }

    [JsonPropertyName("playtime_windows_forever")]
    public int PlaytimeWindowsForever { get; set; }

    [JsonPropertyName("playtime_mac_forever")]
    public int PlaytimeMacForever { get; set; }

    [JsonPropertyName("playtime_linux_forever")]
    public int PlaytimeLinuxForever { get; set; }

    [JsonPropertyName("rtime_last_played")]
    public long RTimeLastPlayed { get; set; }
}
