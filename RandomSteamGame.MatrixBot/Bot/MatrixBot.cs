using RandomSteamGame.MatrixBot.Matrix;
using System.Text.Json;

namespace RandomSteamGame.MatrixBot.Bot;

public class MatrixBot
{
    private readonly MatrixClient.MatrixClient _client;
    private readonly string _roomId;
    private string? _botUserId;

    public MatrixBot(MatrixClient.MatrixClient client, string roomId)
    {
        _client = client;
        _roomId = roomId;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await _client.LoginAsync(ct);

        await _client.JoinRoomAsync(_roomId, ct);
        Console.WriteLine("Joined room successfully!");

        var identity = await _client.WhoAmIAsync(ct);
        _botUserId = identity?.UserId
            ?? throw new InvalidOperationException("Could not determine bot identity.");

        // Establish our starting position
        var initialSync = await _client.SyncAsync(null, ct);
        var since = initialSync?.NextBatch
            ?? throw new InvalidOperationException("Initial sync returned no token.");


        while (!ct.IsCancellationRequested)
        {
            var syncResponse = await _client.SyncAsync(since, ct)
                ?? throw new InvalidOperationException("Sync returned null.");

            await ProcessEventsAsync(syncResponse, ct);

            since = syncResponse.NextBatch
                ?? throw new InvalidOperationException("Sync returned no token.");
        }
    }

    private async Task ProcessEventsAsync(SyncResponse response, CancellationToken ct = default)
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

                if (message?.Sender == _botUserId)
                    continue;

                if (message?.Content?.MessageType != "m.text")
                    continue;


                var body = message?.Content.Body;
                if (body == "!ping")
                {
                    await _client.SendMessageAsync(roomId, "pong", ct);
                }

                if (body?.StartsWith("!steam ", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var identifier = body["!steam ".Length..].Trim();

                    if (!string.IsNullOrWhiteSpace(identifier))
                    {
                        Console.WriteLine($"Steam lookup requested: {identifier}");
                    }
                }

                Console.WriteLine($"{message?.Sender}: {message?.Content.Body}");
            }
        }
    }
}