using System.Text.Json.Serialization;

namespace RandomSteamGame.MatrixBot.Matrix;

public class SyncResponse
{
    [JsonPropertyName("next_batch")]
    public string? NextBatch { get; set; }


}
