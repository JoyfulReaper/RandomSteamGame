using System.Text.Json;
using System.Text.Json.Serialization;

namespace RandomSteamGame.MatrixBot.Matrix;

public class SyncResponse
{
    [JsonPropertyName("next_batch")]
    public string? NextBatch { get; set; }

    [JsonPropertyName("rooms")]
    public SyncRooms? Rooms { get; set; }
}

public class SyncRooms
{
    [JsonPropertyName("join")]
    public Dictionary<string, JoinedRoom>? Join { get; set; }
}

public class JoinedRoom
{
    [JsonPropertyName("timeline")]
    public RoomTimeline? Timeline { get; set; }
}

public class RoomTimeline
{
    [JsonPropertyName("events")]
    public List<JsonElement>? Events { get; set; }

    [JsonPropertyName("limited")]
    public bool Limited { get; set; }

    [JsonPropertyName("prev_batch")]
    public string? PreviousBatch { get; set; }
}