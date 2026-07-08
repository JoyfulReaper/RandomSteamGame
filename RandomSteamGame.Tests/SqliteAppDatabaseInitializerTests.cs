/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.Sqlite;
using Microsoft.Data.Sqlite;

namespace RandomSteamGame.Tests;

public class SqliteAppDatabaseInitializerTests
{
    [Fact]
    public void Initialize_CreatesAppStatsAndVisitorsTablesUnderDataDirectory()
    {
        var databaseFileName = $"kgivler_com.{Guid.NewGuid():N}.db";
        var expectedDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS Visitors (
                IpAddress TEXT PRIMARY KEY,
                Hits INTEGER NOT NULL DEFAULT 1,
                LastSeen TEXT
            );

            CREATE TABLE IF NOT EXISTS AppStats (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                RandomGamesGenerated INTEGER NOT NULL DEFAULT 0
            );
            """;

        try
        {
            var connectionString = SqliteDatabaseInitializer.Initialize(databaseFileName, schemaSql);
            var builder = new SqliteConnectionStringBuilder(connectionString);

            Assert.StartsWith(expectedDirectory, builder.DataSource, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(builder.DataSource));

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            Assert.Equal(1, GetTableCount(connection, "AppStats"));
            Assert.Equal(1, GetTableCount(connection, "Visitors"));
        }
        finally
        {
            var databasePath = Path.Combine(expectedDirectory, databaseFileName);
            SqliteConnection.ClearAllPools();
            DeleteIfExists(databasePath);
            DeleteIfExists(databasePath + "-wal");
            DeleteIfExists(databasePath + "-shm");
        }
    }

    private static long GetTableCount(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";
        command.Parameters.AddWithValue("$tableName", tableName);

        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
