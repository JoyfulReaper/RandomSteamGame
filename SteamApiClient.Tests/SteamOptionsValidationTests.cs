using JoyfulReaperLib.Caching.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SteamApiClient.Settings;
using System.Text.Json;

namespace SteamApiClient.Tests;

public sealed class SteamOptionsValidationTests
{
    private static readonly string[] Policies =
        ["OwnedGames", "AppDetails", "AppDetailsNotFound", "VanitySuccess", "VanityNotFound", "SteamDeckCompatibility"];

    public static IEnumerable<object[]> InvalidCacheDurations() =>
        Policies.SelectMany(policy => new[] { 0, -1, 525601 }.Select(value => new object[] { policy, value }));

    public static IEnumerable<object[]> MissingSections() =>
        new[] { "Steam:Cache", "Steam:RateLimiting" }.Concat(Policies.Select(policy => $"Steam:Cache:{policy}"))
            .Select(path => new object[] { path });

    [Fact]
    public void ShippedConfigurationPassesStartupValidation()
    {
        var values = new Dictionary<string, string?>();
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "steam-validation-appsettings.json")));
        foreach (var property in json.RootElement.GetProperty("Steam").EnumerateObject())
            Flatten(property.Value, $"Steam:{property.Name}");
        // The shipped key is intentionally a placeholder, supplied as a secret at deployment.
        values["Steam:ApiKey"] = new string('A', 32);
        using var provider = CreateProvider(values);
        provider.GetRequiredService<IStartupValidator>().Validate();
        var options = provider.GetRequiredService<IOptions<SteamClientApiOptions>>().Value;
        Assert.Equal(43200, options.Cache.OwnedGames.AbsoluteMinutes);
        Assert.Equal(43200, options.Cache.AppDetails.AbsoluteMinutes);
        Assert.Equal(20, options.RateLimiting.PermitLimit);
        Assert.Equal(10, options.RateLimiting.WindowSeconds);

        void Flatten(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
                foreach (var property in element.EnumerateObject())
                    Flatten(property.Value, $"{path}:{property.Name}");
            else
                values[path] = element.ToString();
        }
    }

    [Theory]
    [MemberData(nameof(InvalidCacheDurations))]
    public void InvalidCacheDurationFailsWithItsPath(string policy, int value)
    {
        var path = $"Steam:Cache:{policy}:AbsoluteMinutes";
        var values = ValidSettings();
        values[path] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var exception = AssertInvalid(values);
        Assert.Contains(path, exception.Message);
        Assert.Contains("between 1 and 525600", exception.Message);
    }

    [Theory]
    [InlineData("PermitLimit", 0)]
    [InlineData("PermitLimit", -1)]
    [InlineData("PermitLimit", 1000001)]
    [InlineData("WindowSeconds", 0)]
    [InlineData("WindowSeconds", -1)]
    [InlineData("WindowSeconds", 86401)]
    public void InvalidRateLimitingFailsWithItsPath(string setting, int value)
    {
        var path = $"Steam:RateLimiting:{setting}";
        var values = ValidSettings();
        values[path] = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Contains(path, AssertInvalid(values).Message);
    }

    [Theory]
    [MemberData(nameof(MissingSections))]
    public void MissingSectionsCannotBeMaskedByInitializers(string path)
    {
        var values = ValidSettings();
        foreach (var key in values.Keys.Where(key => key.StartsWith(path + ":", StringComparison.Ordinal)).ToArray())
            values.Remove(key);
        Assert.Contains(path + " is required.", AssertInvalid(values).Message);
    }

    [Theory]
    [InlineData("Steam:Cache:AppDetailsNotFound:AbsoluteMinutes")]
    [InlineData("Steam:RateLimiting:PermitLimit")]
    [InlineData("Steam:RateLimiting:WindowSeconds")]
    public void UnusableSectionsDoNotSubstituteDefaultValues(string path)
    {
        var values = ValidSettings();
        values.Remove(path);
        values[path[..path.LastIndexOf(':')] + ":UnexpectedSetting"] = "1";
        Assert.Contains(path + " is required.", AssertInvalid(values).Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("STEAM_API_KEY")]
    [InlineData("YOUR_STEAM_API_KEY_HERE")]
    [InlineData("short-secret")]
    public void InvalidApiKeysRemainRejectedWithoutDisclosingValue(string? key)
    {
        var values = ValidSettings();
        values["Steam:ApiKey"] = key;
        var exception = AssertInvalid(values);
        Assert.Contains("Steam:ApiKey", exception.Message);
        if (!string.IsNullOrWhiteSpace(key))
            Assert.DoesNotContain(key, exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankConnectionStringsFail(string? connectionString)
    {
        var values = ValidSettings();
        values["Steam:ConnectionString"] = connectionString;
        Assert.Contains("Steam:ConnectionString must not be blank.", AssertInvalid(values).Message);
    }

    [Fact]
    public void MultipleFailuresAreReportedTogether()
    {
        var values = ValidSettings();
        values["Steam:Cache:OwnedGames:AbsoluteMinutes"] = "0";
        values["Steam:RateLimiting:WindowSeconds"] = "-1";
        var exception = AssertInvalid(values);
        Assert.Equal(2, exception.Failures.Count());
        Assert.Contains("Steam:Cache:OwnedGames:AbsoluteMinutes", exception.Message);
        Assert.Contains("Steam:RateLimiting:WindowSeconds", exception.Message);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(525600, 1000000, 86400)]
    public void InclusiveBoundsPassWithoutOpeningCacheDatabase(int minutes, int permits, int seconds)
    {
        var values = ValidSettings();
        foreach (var policy in Policies)
            values[$"Steam:Cache:{policy}:AbsoluteMinutes"] = minutes.ToString();
        values["Steam:RateLimiting:PermitLimit"] = permits.ToString();
        values["Steam:RateLimiting:WindowSeconds"] = seconds.ToString();
        var path = Path.Combine(Path.GetTempPath(), $"unopened-steam-cache-{Guid.NewGuid():N}.db");
        values["Steam:ConnectionString"] = $"Data Source={path}";
        using var provider = CreateProvider(values);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Equal(values["Steam:ConnectionString"], provider.GetRequiredService<IOptions<SqliteDistributedCacheOptions>>().Value.ConnectionString);
        Assert.False(File.Exists(path));
    }

    private static OptionsValidationException AssertInvalid(Dictionary<string, string?> values)
    {
        using var provider = CreateProvider(values);
        return Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    private static ServiceProvider CreateProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSteamApiClient(configuration);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidSettings()
    {
        var values = new Dictionary<string, string?>
        {
            ["Steam:ApiKey"] = new string('A', 32),
            ["Steam:ConnectionString"] = "Data Source=steam_cache.db",
            ["Steam:RateLimiting:PermitLimit"] = "20",
            ["Steam:RateLimiting:WindowSeconds"] = "10"
        };
        foreach (var policy in Policies)
            values[$"Steam:Cache:{policy}:AbsoluteMinutes"] = "43200";
        return values;
    }
}
