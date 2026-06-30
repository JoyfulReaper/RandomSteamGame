namespace RandomSteamGame.Shared.Contracts;

public sealed record SteamIdentity(
    string? SteamId,
    string? VanityUrl);