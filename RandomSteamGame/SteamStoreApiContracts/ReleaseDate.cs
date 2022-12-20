using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamStoreApiContracts;

public class ReleaseDate
{
    [JsonPropertyName("coming_soon")]
    public bool ComingSoon { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }
}
