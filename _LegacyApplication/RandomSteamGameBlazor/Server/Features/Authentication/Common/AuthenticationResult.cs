namespace RandomSteamGameBlazor.Server.Features.Authentication.Common;

public record AuthenticationResult(
    RandomSteamUser User,
    string Token,
    string RefreshToken);
