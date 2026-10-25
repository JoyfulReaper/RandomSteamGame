using System.Text.Json.Serialization;

namespace RandomSteamGame.MatrixBot.Matrix;

public class Message
{
    [JsonPropertyName("msgtype")]
    public string MsgType { get; set; } = "m.text";

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}