namespace RandomSteamGame.Shared.Contracts.Authentication;

public record LoginRequest(
    string Email,
    string Password);
