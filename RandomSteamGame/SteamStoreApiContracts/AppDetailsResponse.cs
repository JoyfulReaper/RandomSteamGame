using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamStoreApiContracts;

public class AppDetailsResponse
{
    [JsonPropertyName("success")]

    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public AppDetails? Data { get; set; }
}