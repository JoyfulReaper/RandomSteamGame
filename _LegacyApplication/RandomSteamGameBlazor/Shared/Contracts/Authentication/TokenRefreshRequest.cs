namespace RandomSteamGameBlazor.Shared.Contracts.Authentication;
public record TokenRefreshRequest(
    string Token,
    string RefreshToken);