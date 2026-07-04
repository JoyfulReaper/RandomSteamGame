/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Data.Sqlite;
using RandomSteamGame.Persistence;

namespace RandomSteamGame.Tests;

public class SqliteAppDatabaseInitializerTests
{
    [Fact]
    public void Initialize_CreatesDatabaseUnderDataDirectoryAndAppliesSchema()
    {
        var databaseFileName = $"kgivler_com.{Guid.NewGuid():N}.db";
        var expectedDirectory = Path.Combine(AppContext.BaseDirectory, "Data");

        try
        {
            var connectionString = SqliteAppDatabaseInitializer.Initialize(
                databaseFileName,
                "CREATE TABLE IF NOT EXISTS TestTable (Id INTEGER PRIMARY KEY);");
            var builder = new SqliteConnectionStringBuilder(connectionString);

            Assert.StartsWith(expectedDirectory, builder.DataSource, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(builder.DataSource));

            using var connection = new SqliteConnection(connectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TestTable';";

            var tableCount = Convert.ToInt64(command.ExecuteScalar());
            Assert.Equal(1, tableCount);
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

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
