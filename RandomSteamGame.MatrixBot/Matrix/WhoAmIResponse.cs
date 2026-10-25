using System.Text.Json.Serialization;

namespace RandomSteamGame.MatrixBot.Matrix;

public class WhoAmIResponse
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;
}
