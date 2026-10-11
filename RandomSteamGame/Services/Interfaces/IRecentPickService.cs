namespace RandomSteamGame.Services.Interfaces;

public interface IRecentPickService
{
    Task RecordAsync(RecentPick pick, CancellationToken ct = default);
    Task<IReadOnlyList<RecentPick>> GetRecentAsync(int count, CancellationToken ct = default);
}
