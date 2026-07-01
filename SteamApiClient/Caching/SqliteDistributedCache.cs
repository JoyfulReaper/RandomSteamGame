/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;

namespace SteamApiClient.Caching;

internal sealed class SqliteDistributedCache : IDistributedCache
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteDistributedCache> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public SqliteDistributedCache(
        IOptions<SqliteDistributedCacheOptions> options,
        ILogger<SqliteDistributedCache> logger)
    {
        _connectionString = options.Value.ConnectionString;
        _logger = logger;
    }

    public byte[]? Get(string key)
        => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await EnsureInitializedAsync(token);

        await using var connection = CreateConnection();
        await connection.OpenAsync(token);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Value, ExpiresAtTimeUtc, SlidingExpirationInSeconds
            FROM CacheEntries
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", key);

        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);
        if (!await reader.ReadAsync(token))
        {
            return null;
        }

        var expiresAt = reader.IsDBNull(1)
            ? (DateTimeOffset?)null
            : DateTimeOffset.Parse(reader.GetString(1));

        if (IsExpired(expiresAt))
        {
            await RemoveAsync(key, token);
            return null;
        }

        var value = (byte[])reader["Value"];

        if (!reader.IsDBNull(2))
        {
            var slidingExpiration = TimeSpan.FromSeconds(reader.GetInt64(2));
            await RefreshAsync(key, token, slidingExpiration);
        }

        return value;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(options);

        await EnsureInitializedAsync(token);

        var now = DateTimeOffset.UtcNow;
        var absoluteExpiration = GetAbsoluteExpiration(now, options);
        var slidingExpiration = options.SlidingExpiration;
        var expiresAt = absoluteExpiration;

        if (slidingExpiration.HasValue)
        {
            var slidingExpiresAt = now.Add(slidingExpiration.Value);
            expiresAt = expiresAt.HasValue && expiresAt.Value < slidingExpiresAt
                ? expiresAt
                : slidingExpiresAt;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync(token);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CacheEntries (
                Id,
                Value,
                ExpiresAtTimeUtc,
                SlidingExpirationInSeconds,
                AbsoluteExpirationUtc)
            VALUES (
                $id,
                $value,
                $expiresAt,
                $slidingExpirationSeconds,
                $absoluteExpiration)
            ON CONFLICT(Id) DO UPDATE SET
                Value = excluded.Value,
                ExpiresAtTimeUtc = excluded.ExpiresAtTimeUtc,
                SlidingExpirationInSeconds = excluded.SlidingExpirationInSeconds,
                AbsoluteExpirationUtc = excluded.AbsoluteExpirationUtc;
            """;
        command.Parameters.AddWithValue("$id", key);
        command.Parameters.Add("$value", SqliteType.Blob).Value = value;
        command.Parameters.AddWithValue("$expiresAt", ToDbValue(expiresAt));
        command.Parameters.AddWithValue(
            "$slidingExpirationSeconds",
            slidingExpiration.HasValue ? slidingExpiration.Value.TotalSeconds : DBNull.Value);
        command.Parameters.AddWithValue("$absoluteExpiration", ToDbValue(absoluteExpiration));

        await command.ExecuteNonQueryAsync(token);
        await DeleteExpiredEntriesAsync(connection, token);
    }

    public void Refresh(string key)
        => RefreshAsync(key).GetAwaiter().GetResult();

    public Task RefreshAsync(string key, CancellationToken token = default)
        => RefreshAsync(key, token, slidingExpirationOverride: null);

    public void Remove(string key)
        => RemoveAsync(key).GetAwaiter().GetResult();

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await EnsureInitializedAsync(token);

        await using var connection = CreateConnection();
        await connection.OpenAsync(token);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CacheEntries WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", key);
        await command.ExecuteNonQueryAsync(token);
    }

    private async Task RefreshAsync(
        string key,
        CancellationToken token,
        TimeSpan? slidingExpirationOverride)
    {
        await EnsureInitializedAsync(token);

        await using var connection = CreateConnection();
        await connection.OpenAsync(token);

        await using var selectCommand = connection.CreateCommand();
        selectCommand.CommandText = """
            SELECT AbsoluteExpirationUtc, SlidingExpirationInSeconds
            FROM CacheEntries
            WHERE Id = $id;
            """;
        selectCommand.Parameters.AddWithValue("$id", key);

        await using var reader = await selectCommand.ExecuteReaderAsync(CommandBehavior.SingleRow, token);
        if (!await reader.ReadAsync(token))
        {
            return;
        }

        var absoluteExpiration = reader.IsDBNull(0)
            ? (DateTimeOffset?)null
            : DateTimeOffset.Parse(reader.GetString(0));

        var slidingExpiration = slidingExpirationOverride ?? (
            reader.IsDBNull(1)
                ? (TimeSpan?)null
                : TimeSpan.FromSeconds(reader.GetInt64(1)));

        if (!slidingExpiration.HasValue)
        {
            return;
        }

        var newExpiration = DateTimeOffset.UtcNow.Add(slidingExpiration.Value);
        if (absoluteExpiration.HasValue && absoluteExpiration.Value < newExpiration)
        {
            newExpiration = absoluteExpiration.Value;
        }

        await using var updateCommand = connection.CreateCommand();
        updateCommand.CommandText = """
            UPDATE CacheEntries
            SET ExpiresAtTimeUtc = $expiresAt
            WHERE Id = $id;
            """;
        updateCommand.Parameters.AddWithValue("$id", key);
        updateCommand.Parameters.AddWithValue("$expiresAt", ToDbValue(newExpiration));
        await updateCommand.ExecuteNonQueryAsync(token);
    }

    private async Task EnsureInitializedAsync(CancellationToken token)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(token);
        try
        {
            if (_initialized)
            {
                return;
            }

            var builder = new SqliteConnectionStringBuilder(_connectionString);
            var dataSource = builder.DataSource;
            if (!string.IsNullOrWhiteSpace(dataSource) && !Path.IsPathRooted(dataSource))
            {
                var dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
                Directory.CreateDirectory(dataFolder);
                builder.DataSource = Path.Combine(dataFolder, dataSource);
                _logger.LogInformation("Using SQLite distributed cache database at {CachePath}", builder.DataSource);
                await using var initConnection = new SqliteConnection(builder.ConnectionString);
                await initConnection.OpenAsync(token);
                await CreateSchemaAsync(initConnection, token);
                _initialized = true;
                return;
            }

            if (!string.IsNullOrWhiteSpace(dataSource))
            {
                var directory = Path.GetDirectoryName(dataSource);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }

            await using var connection = CreateConnection();
            await connection.OpenAsync(token);
            await CreateSchemaAsync(connection, token);
            _initialized = true;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;

            CREATE TABLE IF NOT EXISTS CacheEntries (
                Id TEXT NOT NULL PRIMARY KEY,
                Value BLOB NOT NULL,
                ExpiresAtTimeUtc TEXT NULL,
                SlidingExpirationInSeconds INTEGER NULL,
                AbsoluteExpirationUtc TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_CacheEntries_ExpiresAtTimeUtc
            ON CacheEntries (ExpiresAtTimeUtc);
            """;

        await command.ExecuteNonQueryAsync(token);
    }

    private async Task DeleteExpiredEntriesAsync(SqliteConnection connection, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM CacheEntries
            WHERE ExpiresAtTimeUtc IS NOT NULL
              AND ExpiresAtTimeUtc <= $utcNow;
            """;
        command.Parameters.AddWithValue("$utcNow", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);
    }

    private SqliteConnection CreateConnection()
        => new(_connectionString);

    private static DateTimeOffset? GetAbsoluteExpiration(
        DateTimeOffset now,
        DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpirationRelativeToNow.HasValue)
        {
            return now.Add(options.AbsoluteExpirationRelativeToNow.Value);
        }

        return options.AbsoluteExpiration;
    }

    private static object ToDbValue(DateTimeOffset? value)
        => value?.ToString("O") ?? (object)DBNull.Value;

    private static bool IsExpired(DateTimeOffset? expiresAt)
        => expiresAt.HasValue && expiresAt.Value <= DateTimeOffset.UtcNow;
}
