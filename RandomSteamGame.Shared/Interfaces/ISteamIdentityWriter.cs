namespace RandomSteamGame.Shared.Interfaces;

public interface ISteamIdentityWriter
{
    Task SetIdentityAsync(long steamId, string? vanityUrl);
    Task ClearAsync();
}
