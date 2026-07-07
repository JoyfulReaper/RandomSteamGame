/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SteamApiClient.Tests;

public class SteamApiDependencyInjectionTests
{
    [Fact]
    public void AddSteamApiClient_RegistersHybridCacheAndSqliteDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSteamApiClient(CreateConfiguration());

        using var provider = services.BuildServiceProvider();

        var hybridCache = provider.GetRequiredService<HybridCache>();
        var distributedCache = provider.GetRequiredService<IDistributedCache>();

        Assert.NotNull(hybridCache);
        Assert.Equal("JoyfulReaperLib.Caching.Sqlite.SqliteDistributedCache", distributedCache.GetType().FullName);
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Steam:ApiKey"] = new string('A', 32),
                ["Steam:ConnectionString"] = "Data Source=steam_cache.db",
                ["Steam:Cache:OwnedGames:AbsoluteMinutes"] = "60",
                ["Steam:Cache:AppDetails:AbsoluteMinutes"] = "60",
                ["Steam:Cache:VanitySuccess:AbsoluteMinutes"] = "120",
                ["Steam:Cache:VanityNotFound:AbsoluteMinutes"] = "15",
                ["Steam:RateLimiting:PermitLimit"] = "20",
                ["Steam:RateLimiting:WindowSeconds"] = "10"
            })
            .Build();
    }
}
