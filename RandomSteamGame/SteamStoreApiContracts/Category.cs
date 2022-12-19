using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamStoreApiContracts;

public class Category
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = default!;
}
