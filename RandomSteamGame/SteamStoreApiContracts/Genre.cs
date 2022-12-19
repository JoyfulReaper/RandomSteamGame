namespace RandomSteamGame.SteamStoreApiContracts;
using System.Text.Json.Serialization;

public class Genre
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("description")]
    public string Description { get; set; } = default!;
}
