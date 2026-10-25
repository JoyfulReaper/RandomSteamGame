namespace RandomSteamGame.MatrixBot.Matrix;

using System.Text.Json.Serialization;

public class MatrixMessageEvent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sender")]
    public string? Sender { get; set; }

    [JsonPropertyName("content")]
    public MatrixMessageContent? Content { get; set; }
}

public class MatrixMessageContent
{
    [JsonPropertyName("msgtype")]
    public string? MessageType { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }
}