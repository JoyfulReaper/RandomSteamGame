namespace RandomSteamGame.Options;

public sealed class TelemetryOptions
{
    public const string SectionName = "Telemetry";

    public string VisitorHashKey { get; set; } = string.Empty;
}