using System.Globalization;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Events;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Services;

public sealed class SqliteRecentPickService(string connectionString) : IRecentPickService
{
    private static readonly SemaphoreSlim DatabaseGate = new(1, 1);

    internal const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS RecentPicks (
            AppId INTEGER NOT NULL,
            GameName TEXT NOT NULL,
            PickedAtUtc TEXT NOT NULL
        );
        """;

    public async Task RecordAsync(RecentPick pick, CancellationToken ct = default)
    {
        await DatabaseGate.WaitAsync(ct);
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = connection.BeginTransaction(deferred: false);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO RecentPicks (AppId, GameName, PickedAtUtc)
                VALUES ($appId, $gameName, $pickedAtUtc);

                DELETE FROM RecentPicks WHERE rowid NOT IN (
                    SELECT rowid FROM RecentPicks
                    ORDER BY PickedAtUtc DESC, rowid DESC LIMIT 20
                );
                """;
            command.Parameters.AddWithValue("$appId", pick.AppId);
            command.Parameters.AddWithValue("$gameName", GamePickTelemetryName.Sanitize(pick.GameName) ?? "Unknown game");
            command.Parameters.AddWithValue("$pickedAtUtc", pick.PickedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally
        {
            DatabaseGate.Release();
        }
    }

    public async Task<IReadOnlyList<RecentPick>> GetRecentAsync(int count, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 0)
        {
            return [];
        }

        await DatabaseGate.WaitAsync(ct);
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT AppId, GameName, PickedAtUtc FROM RecentPicks
                ORDER BY PickedAtUtc DESC, rowid DESC LIMIT $count;
                """;
            command.Parameters.AddWithValue("$count", count);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var picks = new List<RecentPick>();
            while (await reader.ReadAsync(ct))
            {
                picks.Add(new RecentPick(reader.GetInt32(0), reader.GetString(1),
                    DateTimeOffset.ParseExact(reader.GetString(2), "O", CultureInfo.InvariantCulture)));
            }

            return picks;
        }
        finally
        {
            DatabaseGate.Release();
        }
    }

    // A Blazor scope can outlive a request. Never share a connection between calls.
    // Optional history should only wait briefly for another SQLite writer.
    private SqliteConnection CreateConnection() => new(connectionString) { DefaultTimeout = 1 };
}
