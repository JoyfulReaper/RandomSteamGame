namespace RandomSteamGameBlazor.Shared.Contracts.Authentication;

public record AuthenticationResponse(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Token,
    string RefreshToken,
    string? SteamId,
    string? VanityUrl);