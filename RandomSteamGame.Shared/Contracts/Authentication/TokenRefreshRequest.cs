namespace RandomSteamGame.Shared.Contracts.Authentication;

public record TokenRefreshRequest(
    string Token,
    string RefreshToken);