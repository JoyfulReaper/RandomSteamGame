using System.Text.Json.Serialization;

namespace RandomSteamGame.MatrixBot.Matrix;

public class LoginResponse
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("home_server")]
    public string? HomeServer { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("well_known")]
    public WellKnown? WellKnown { get; set; }
}

public class WellKnown
{
    [JsonPropertyName("m.homeserver")]
    public HomeserverInfo? Homeserver { get; set; }
}

public class HomeserverInfo
{
    [JsonPropertyName("base_url")]
    public string? BaseUrl { get; set; }
}