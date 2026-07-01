namespace RandomSteamGame.Services.Interfaces;

public interface IBetaAvailabilityService
{
    Task<bool> IsBetaAvailableAsync(CancellationToken cancellationToken = default);
}
