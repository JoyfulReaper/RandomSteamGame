namespace RandomSteamGame.Services.Interfaces;

public interface ILibraryExportCooldownTracker
{
    TimeSpan? GetRetryAfter(string partitionKey);

    void MarkSucceeded(string partitionKey);
}