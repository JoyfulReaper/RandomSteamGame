using RandomSteamGame.Services;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Tests;

internal sealed class TestRecentPickService : IRecentPickService
{
    public List<RecentPick> Picks { get; } = [];
    public Exception? RecordFailure { get; init; }
    public Exception? LoadFailure { get; init; }
    public Action? OnRecord { get; init; }
    public int RecordCalls { get; private set; }
    public int? RequestedCount { get; private set; }

    public Task RecordAsync(RecentPick pick, CancellationToken ct = default)
    {
        RecordCalls++;
        OnRecord?.Invoke();
        if (RecordFailure is not null)
        {
            return Task.FromException(RecordFailure);
        }
        Picks.Add(pick);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RecentPick>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        RequestedCount = count;
        return LoadFailure is not null
            ? Task.FromException<IReadOnlyList<RecentPick>>(LoadFailure)
            : Task.FromResult<IReadOnlyList<RecentPick>>(Picks.Take(count).ToArray());
    }
}
