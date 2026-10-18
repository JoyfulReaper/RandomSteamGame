using RandomSteamGame.MatrixBot.Matrix;
using RandomSteamGame.SteamClient;
using System.Text.Json;

namespace RandomSteamGame.MatrixBot.Bot;

public class MatrixBot
{
    private readonly MatrixClient.MatrixClient _client;
    private readonly RandomSteamClient _steamClient;
    private readonly string _roomId;
    private string? _botUserId;

    public MatrixBot(MatrixClient.MatrixClient client, string roomId, RandomSteamClient steamClient)
    {
        _client = client;
        _roomId = roomId;
        _steamClient = steamClient;
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

                    if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 100)
                    {
                        await _client.SendMessageAsync(
                            roomId,
                            "Usage: !steam <SteamID or vanity name>",
                            ct);

                        continue;
                    }

                    try
                    {
                        var game = await _steamClient.GetRandomGameAsync(identifier, ct);

                        if (game is null)
                        {
                            await _client.SendMessageAsync(
                                roomId,
                                "Couldn't find a game for that Steam account.",
                                ct);

                            continue;
                        }

                        var reply = $"🎲 You should play: {game.Name}\n" +
                                    $"https://store.steampowered.com/app/{game.Id}/";

                        await _client.SendMessageAsync(roomId, reply, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Steam lookup failed: {ex}");

                        await _client.SendMessageAsync(
                            roomId,
                            "Sorry, I couldn't pick a game right now.",
                            ct);
                    }
                }

                Console.WriteLine($"{message?.Sender}: {message?.Content.Body}");
            }
        }
    }
}