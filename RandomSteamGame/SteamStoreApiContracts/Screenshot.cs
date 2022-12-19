using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamStoreApiContracts;


public class Screenshot
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("path_thumbnail")]
    public string PathThumbnail { get; set; } = default!;

    [JsonPropertyName("path_full")]
    public string PathFull { get; set; } = default!;
}