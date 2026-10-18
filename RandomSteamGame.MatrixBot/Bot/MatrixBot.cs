using RandomSteamGame.MatrixBot.Matrix;
using System.Text.Json;

namespace RandomSteamGame.MatrixBot.Bot;

public class MatrixBot
{
    private readonly MatrixClient.MatrixClient _client;

    public MatrixBot(MatrixClient.MatrixClient client)
    {
        _client = client;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await _client.LoginAsync(ct);

        await _client.JoinRoomAsync("!NTvCQirbQTeqzUiSoh:kgivler.com", ct);
        Console.WriteLine("Joined room successfully!");

        // Establish our starting position
        var initialSync = await _client.SyncAsync(null, ct);
        var since = initialSync?.NextBatch
            ?? throw new InvalidOperationException("Initial sync returned no token.");

        while (!ct.IsCancellationRequested)
        {
            var syncResponse = await _client.SyncAsync(since, ct)
                ?? throw new InvalidOperationException("Sync returned null.");

            ProcessEvents(syncResponse);

            since = syncResponse.NextBatch
                ?? throw new InvalidOperationException("Sync returned no token.");
        }
    }

    private void ProcessEvents(SyncResponse response)
    {
        foreach (var (roomId, room) in response?.Rooms?.Join
                 ?? new Dictionary<string, JoinedRoom>())
        {
            foreach (var evt in room.Timeline?.Events ?? [])
            {
                if (!evt.TryGetProperty("type", out var type) ||
                    type.GetString() != "m.room.message")
                    continue;

                var message = evt.Deserialize<MatrixMessageEvent>();

                if (message?.Content?.MessageType != "m.text")
                    continue;

                Console.WriteLine($"{message.Sender}: {message.Content.Body}");
            }
        }
    }
}