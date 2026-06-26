namespace RandomSteamGameBlazor.Shared.Contracts.RandomSteamGame;

public record OwnedGamesResponse(
    long SteamId,
    int GameCount,
    List<Game> Games);

public record Game(
    int AppId,
    string? Name,
    int PlaytimeForever,
    string? ImgIconUrl,
    int PlaytimeWindowsForever,
    int PlaytimeMacForever,
    int PlaytimeLinuxForever,
    long RTimeLastPlayed,
    int Playtime2Weeks);