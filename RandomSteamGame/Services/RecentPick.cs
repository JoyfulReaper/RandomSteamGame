namespace RandomSteamGame.Services;

// Anonymous display metadata only; never add request, visitor, or library data here.
public sealed record RecentPick(int AppId, string GameName, DateTimeOffset PickedAtUtc);
