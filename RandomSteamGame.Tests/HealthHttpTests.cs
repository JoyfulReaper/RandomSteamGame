using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;

namespace RandomSteamGame.Tests;

[Collection(nameof(ServerBrowserExecutionTests))]
public sealed class HealthHttpTests : IClassFixture<SeoWebApplicationFactory>
{
    private readonly SeoWebApplicationFactory _factory;

    public HealthHttpTests(SeoWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("initialized", HttpStatusCode.OK)]
    [InlineData("blank", HttpStatusCode.ServiceUnavailable)]
    [InlineData("readonly", HttpStatusCode.ServiceUnavailable)]
    public async Task LivenessRemainsIndependentOfApplicationPersistence(string databaseState, HttpStatusCode readyStatus)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"readiness-http-{Guid.NewGuid():N}.db");
        try
        {
            if (databaseState != "blank")
            {
                await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = LocalReadinessHealthCheckTests.Schema;
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            using var application = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<SqliteConnection>();
                services.AddScoped(_ => new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = databaseState == "readonly" ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
                    Pooling = false
                }.ToString()));
            }));
            using var client = application.CreateClient();
            using var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal("Healthy", await live.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
            Assert.Equal(readyStatus, ready.StatusCode);
            Assert.Equal(readyStatus == HttpStatusCode.OK ? "Healthy" : "Unhealthy",
                await ready.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
