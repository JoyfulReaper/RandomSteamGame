using System.Text.Json.Serialization;

namespace RandomSteamGame.Events;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ApplicationStartedEvent))]
[JsonSerializable(typeof(GamePickCompletedEvent))]
[JsonSerializable(typeof(LibraryExportCompletedEvent))]
[JsonSerializable(typeof(SiteVisitRecordedEvent))]
[JsonSerializable(typeof(LibraryExportRejectedEvent))]
public partial class RandomSteamGameJsonContext : JsonSerializerContext
{
}