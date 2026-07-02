using RandomSteamGame.Services;
using SteamApiClient.Contracts.SteamApi;

namespace RandomSteamGame.Tests;

public class SteamProviderTests
{
    [Fact]
    public void IsUnplayed_ReturnsTrue_WhenAllSteamPlaySignalsAreZero()
    {
        var game = CreateGame();

        Assert.True(SteamProvider.IsUnplayed(game));
    }

    [Theory]
    [InlineData(15, 0, 0, 0, 0, 0)]
    [InlineData(0, 20, 0, 0, 0, 0)]
    [InlineData(0, 0, 25, 0, 0, 0)]
    [InlineData(0, 0, 0, 30, 0, 0)]
    [InlineData(0, 0, 0, 0, 35, 0)]
    [InlineData(0, 0, 0, 0, 0, 1234567890)]
    public void IsUnplayed_ReturnsFalse_WhenAnySteamPlaySignalShowsActivity(
        int playtimeForever,
        int playtimeWindowsForever,
        int playtimeMacForever,
        int playtimeLinuxForever,
        int playtime2Weeks,
        long rTimeLastPlayed)
    {
        var game = CreateGame(
            playtimeForever: playtimeForever,
            playtimeWindowsForever: playtimeWindowsForever,
            playtimeMacForever: playtimeMacForever,
            playtimeLinuxForever: playtimeLinuxForever,
            playtime2Weeks: playtime2Weeks,
            rTimeLastPlayed: rTimeLastPlayed);

        Assert.False(SteamProvider.IsUnplayed(game));
    }

    [Theory]
    [InlineData(120, 0, 0, 0, 0, 120)]
    [InlineData(0, 80, 40, 0, 0, 120)]
    [InlineData(60, 80, 40, 0, 0, 120)]
    [InlineData(0, 0, 0, 0, 45, 45)]
    public void GetDisplayPlaytimeMinutes_UsesBestAvailableSteamPlaytimeSignal(
        int playtimeForever,
        int playtimeWindowsForever,
        int playtimeMacForever,
        int playtimeLinuxForever,
        int playtime2Weeks,
        int expectedMinutes)
    {
        var game = CreateGame(
            playtimeForever: playtimeForever,
            playtimeWindowsForever: playtimeWindowsForever,
            playtimeMacForever: playtimeMacForever,
            playtimeLinuxForever: playtimeLinuxForever,
            playtime2Weeks: playtime2Weeks);

        Assert.Equal(expectedMinutes, SteamProvider.GetDisplayPlaytimeMinutes(game));
    }

    private static Game CreateGame(
        int playtimeForever = 0,
        int playtimeWindowsForever = 0,
        int playtimeMacForever = 0,
        int playtimeLinuxForever = 0,
        int playtime2Weeks = 0,
        long rTimeLastPlayed = 0)
    {
        return new Game(
            AppId: 10,
            Name: "Test Game",
            PlaytimeForever: playtimeForever,
            ImgIconUrl: null,
            PlaytimeWindowsForever: playtimeWindowsForever,
            PlaytimeMacForever: playtimeMacForever,
            PlaytimeLinuxForever: playtimeLinuxForever,
            RTimeLastPlayed: rTimeLastPlayed,
            Playtime2Weeks: playtime2Weeks);
    }
}
