/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace SteamApiClient.Tests;

public class SteamVanityUrlHelperTests
{
    [Theory]
    [InlineData("Mister_God")]
    [InlineData("Mister_god")]
    [InlineData("mister_god")]
    [InlineData("https://steamcommunity.com/id/Mister_God/")]
    [InlineData("http://steamcommunity.com/id/Mister_God")]
    [InlineData("steamcommunity.com/id/Mister_God")]
    [InlineData("www.steamcommunity.com/id/Mister_God/")]
    [InlineData("https:%2F%2Fsteamcommunity.com%2Fid%2FMister_God%2F")]
    public void Normalize_ProducesCanonicalVanity(string input)
    {
        var normalized = SteamVanityUrlHelper.Normalize(input);

        Assert.Equal("mister_god", normalized);
    }

    [Theory]
    [InlineData("Mister_God")]
    [InlineData("Mister_god")]
    [InlineData("mister_god")]
    [InlineData("https://steamcommunity.com/id/Mister_God/")]
    [InlineData("https:%2F%2Fsteamcommunity.com%2Fid%2FMister_God%2F")]
    public void BuildCacheKey_UsesCanonicalNormalizedVanity(string input)
    {
        var normalized = SteamVanityUrlHelper.Normalize(input);
        var cacheKey = SteamVanityUrlHelper.BuildCacheKey(normalized);

        Assert.Equal("vanity:v2:success:mister_god", cacheKey);
        Assert.Equal("vanity:v2:notfound:mister_god", SteamVanityUrlHelper.BuildNotFoundCacheKey(normalized));
    }

    [Theory]
    [InlineData("not_found_foo", "foo")]
    [InlineData("not_found_not_found_foo", "not_found_foo")]
    public void VanityCacheNamespaces_AreDisjoint(string successVanity, string missingVanity)
    {
        var successKey = SteamVanityUrlHelper.BuildCacheKey(SteamVanityUrlHelper.Normalize(successVanity));
        var notFoundKey = SteamVanityUrlHelper.BuildNotFoundCacheKey(SteamVanityUrlHelper.Normalize(missingVanity));

        Assert.NotEqual(successKey, notFoundKey);
        Assert.NotEqual(successKey, SteamVanityUrlHelper.BuildNotFoundCacheKey(successVanity));
        Assert.NotEqual($"vanity_{successVanity}", successKey);
        Assert.NotEqual($"vanity_not_found_{missingVanity}", notFoundKey);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("https://steamcommunity.com/profiles/76561197960287930/")]
    [InlineData("https://example.com/id/Mister_God/")]
    [InlineData("https://steamcommunity.com/id//")]
    public void Normalize_InvalidInput_ThrowsArgumentException(string input)
    {
        Assert.Throws<ArgumentException>(() => SteamVanityUrlHelper.Normalize(input));
    }
}
