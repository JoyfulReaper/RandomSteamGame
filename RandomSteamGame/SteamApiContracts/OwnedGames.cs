using System.Text.Json.Serialization;

namespace RandomSteamGame.SteamApiContracts;

public sealed class OwnedGames
{
    [JsonPropertyName("game_count")]
    public int GameCount { get; set; }

    [JsonPropertyName("games")]
    public List<Game> Games { get; set; } = new List<Game>();
}
