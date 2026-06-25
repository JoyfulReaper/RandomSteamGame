namespace RandomSteamGameBlazor.Shared.Contracts.Authentication;

public record LoginRequest(
    string Email,
    string Password);
