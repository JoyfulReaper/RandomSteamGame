using System.Globalization;
using Microsoft.Data.Sqlite;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services;

public sealed class SqliteFeatureVotingService(string connectionString) : IFeatureVotingService
{
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private const string ProposalSchemaSql = """
        CREATE TABLE IF NOT EXISTS FeatureProposals (
            Id INTEGER PRIMARY KEY,
            Slug TEXT NOT NULL UNIQUE CHECK (
                length(Slug) BETWEEN 1 AND 80 AND Slug NOT GLOB '*[^a-z0-9-]*'
                AND Slug NOT LIKE '-%' AND Slug NOT LIKE '%-'),
            Title TEXT NOT NULL CHECK (length(trim(Title)) BETWEEN 1 AND 160),
            Description TEXT NOT NULL CHECK (length(trim(Description)) BETWEEN 1 AND 4000),
            Status TEXT NOT NULL CHECK (Status IN ('open', 'planned', 'completed', 'closed')),
            GitHubIssueNumber INTEGER CHECK (GitHubIssueNumber > 0)
        );
        """;
    internal const string SchemaSql = ProposalSchemaSql + """
        CREATE TABLE IF NOT EXISTS FeatureVotes (
            FeatureProposalId INTEGER NOT NULL REFERENCES FeatureProposals(Id),
            BrowserIdentityHash TEXT NOT NULL CHECK (
                length(BrowserIdentityHash) = 64 AND BrowserIdentityHash NOT GLOB '*[^0-9a-f]*'),
            CreatedAtUtc TEXT NOT NULL,
            PRIMARY KEY (FeatureProposalId, BrowserIdentityHash)
        );
        """;

    internal static void InitializeSchema(string connectionString)
    {
        // CHECK constraints cannot be widened with ALTER COLUMN. Upgrade only the known
        // original constraint using a transactional copy-and-swap, never a data reset.
        // Foreign keys are disabled only on this non-pooled startup connection so replacing
        // the parent definition cannot delete/repoint votes. Verify references before commit.
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder(connectionString)
        {
            Cache = SqliteCacheMode.Default, ForeignKeys = false, Pooling = false
        }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction(deferred: false);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = SchemaSql;
        command.ExecuteNonQuery();
        command.CommandText = "SELECT sql FROM sqlite_schema WHERE type = 'table' AND name = 'FeatureProposals';";
        var schema = (string)command.ExecuteScalar()!;
        if (schema.Contains("Status IN ('open', 'closed')", StringComparison.Ordinal))
        {
            // Preserve user-added indexes/triggers as well as the built-in UNIQUE constraint.
            command.CommandText = "SELECT sql FROM sqlite_schema WHERE tbl_name = 'FeatureProposals' AND type IN ('index', 'trigger') AND sql IS NOT NULL;";
            var objects = new List<string>();
            using (var reader = command.ExecuteReader())
                while (reader.Read()) objects.Add(reader.GetString(0));

            command.CommandText = ProposalSchemaSql.Replace("CREATE TABLE IF NOT EXISTS FeatureProposals",
                "CREATE TABLE FeatureProposals_StatusUpgrade", StringComparison.Ordinal);
            command.ExecuteNonQuery();
            // SELECT * deliberately fails rather than losing any unexpected extra columns.
            command.CommandText = "INSERT INTO FeatureProposals_StatusUpgrade SELECT * FROM FeatureProposals;";
            command.ExecuteNonQuery();
            command.CommandText = "SELECT COUNT(*) FROM (SELECT * FROM FeatureProposals EXCEPT SELECT * FROM FeatureProposals_StatusUpgrade);";
            if (Convert.ToInt64(command.ExecuteScalar()) != 0)
                throw new InvalidOperationException("Feature proposal schema upgrade did not preserve every row.");
            command.CommandText = """
                DROP TABLE FeatureProposals;
                ALTER TABLE FeatureProposals_StatusUpgrade RENAME TO FeatureProposals;
                """;
            command.ExecuteNonQuery();
            foreach (var sql in objects)
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
            command.CommandText = "PRAGMA foreign_key_check;";
            using var violations = command.ExecuteReader();
            if (violations.Read())
                throw new InvalidOperationException("Feature proposal schema upgrade found invalid foreign key references.");
        }
        transaction.Commit();
    }

    public async Task<IReadOnlyList<FeatureProposalResponse>> GetAsync(string browserHash, CancellationToken ct)
    {
        ValidateHash(browserHash);
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.Id, p.Slug, p.Title, p.Description, p.Status, p.GitHubIssueNumber,
                (SELECT COUNT(*) FROM FeatureVotes v WHERE v.FeatureProposalId = p.Id),
                EXISTS (SELECT 1 FROM FeatureVotes v
                    WHERE v.FeatureProposalId = p.Id AND v.BrowserIdentityHash = $browser)
            FROM FeatureProposals p ORDER BY p.Id;
            """;
        command.Parameters.AddWithValue("$browser", browserHash);
        var proposals = new List<FeatureProposalResponse>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            proposals.Add(new(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetInt64(5),
                reader.GetInt64(6), reader.GetBoolean(7)));
        }
        return proposals;
    }

    public async Task<FeatureVoteResult> SetSupportedAsync(long featureId, string browserHash, bool supported, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(featureId);
        ValidateHash(browserHash);
        // The SQLite provider's busy wait is synchronous. Serialize this process's voting
        // writers asynchronously, with bounded admission, rather than tying up request threads.
        if (!await WriteGate.WaitAsync(TimeSpan.FromSeconds(1), ct))
            throw new TimeoutException("Feature voting write admission timed out.");
        try { return await WriteAsync(featureId, browserHash, supported, ct); }
        finally { WriteGate.Release(); }
    }

    private async Task<FeatureVoteResult> WriteAsync(long featureId, string browserHash, bool supported, CancellationToken ct)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);
        // SQLite serializes writers across connections/processes. Check status and change the
        // vote in one short immediate transaction, independent of the recent-pick semaphore.
        using var transaction = connection.BeginTransaction(deferred: false);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Status FROM FeatureProposals WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", featureId);
        var status = await command.ExecuteScalarAsync(ct);
        if (status is null)
            return FeatureVoteResult.NotFound;
        if (!string.Equals(status as string, "open", StringComparison.Ordinal))
            return FeatureVoteResult.Closed;

        command.Parameters.AddWithValue("$browser", browserHash);
        if (supported)
        {
            command.CommandText = """
                INSERT INTO FeatureVotes (FeatureProposalId, BrowserIdentityHash, CreatedAtUtc)
                VALUES ($id, $browser, $created)
                ON CONFLICT (FeatureProposalId, BrowserIdentityHash) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
        else
        {
            command.CommandText = "DELETE FROM FeatureVotes WHERE FeatureProposalId = $id AND BrowserIdentityHash = $browser;";
        }
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return FeatureVoteResult.Saved;
    }

    private SqliteConnection CreateConnection() => new(new SqliteConnectionStringBuilder(connectionString)
    {
        ForeignKeys = true,
        Cache = SqliteCacheMode.Default
    }.ToString()) { DefaultTimeout = 1 };

    private static void ValidateHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("Expected a SHA-256 browser identity hash.", nameof(hash));
    }
}
