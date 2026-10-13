namespace RandomSteamGame.Services;

// Singleton synchronization: atomic within one application process, not across processes.
public sealed class OwnedGamesRefreshAdmissionCoordinator
{
    private readonly object _gate = new();
    private readonly Dictionary<long, Entry> _entries = [];

    internal int ActiveKeyCount
    {
        get { lock (_gate) return _entries.Count; }
    }

    internal async Task<IDisposable> AcquireAsync(long steamId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Entry entry;
        // The shared gate protects only bookkeeping, never cache operations or waiting.
        lock (_gate)
        {
            if (!_entries.TryGetValue(steamId, out entry!))
                _entries.Add(steamId, entry = new Entry());
            entry.References++;
        }

        try
        {
            await entry.Semaphore.WaitAsync(ct);
            return new Lease(this, steamId, entry);
        }
        catch
        {
            ReleaseReference(steamId, entry);
            throw;
        }
    }

    private void ReleaseReference(long steamId, Entry entry)
    {
        lock (_gate)
        {
            if (--entry.References == 0)
            {
                _entries.Remove(steamId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class Entry
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
        public int References { get; set; }
    }

    private sealed class Lease(OwnedGamesRefreshAdmissionCoordinator owner, long steamId, Entry entry) : IDisposable
    {
        private OwnedGamesRefreshAdmissionCoordinator? _owner = owner;

        public void Dispose()
        {
            var coordinator = Interlocked.Exchange(ref _owner, null);
            if (coordinator is null) return;
            entry.Semaphore.Release();
            coordinator.ReleaseReference(steamId, entry);
        }
    }
}
