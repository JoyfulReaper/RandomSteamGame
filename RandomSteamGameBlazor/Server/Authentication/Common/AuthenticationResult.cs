namespace RandomSteamGameBlazor.Server.Authentication.Common;

public record AuthenticationResult(
    RandomSteamUser User,
    string Token,
    string RefreshToken);
