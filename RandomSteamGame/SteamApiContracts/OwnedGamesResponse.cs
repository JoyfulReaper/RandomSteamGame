using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamApiContracts;

public class OwnedGamesResponse
{
    [JsonPropertyName("response")]
    public OwnedGames Response { get; set; } = default!;
}
