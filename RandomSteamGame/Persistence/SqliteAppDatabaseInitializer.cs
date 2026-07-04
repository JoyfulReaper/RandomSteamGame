/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.Caching.Sqlite;
using Microsoft.Data.Sqlite;

namespace RandomSteamGame.Persistence;

public static class SqliteAppDatabaseInitializer
{
    public static string Initialize(string dbFileName, string schemaSql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaSql);

        SqliteProviderInitializer.Initialize();

        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);

        var databasePath = Path.GetFullPath(Path.Combine(dataDirectory, dbFileName));
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = schemaSql;
        command.ExecuteNonQuery();

        return connectionString;
    }
}
