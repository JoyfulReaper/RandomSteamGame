using RandomSteamGame.Services;
using RandomSteamGame.Shared.Contracts;
using System.Text;
using SteamDeckCompatibilityCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Tests;

public class SteamLibraryExportServiceTests
{
    [Fact]
    public void Export_EscapesNamesWithCommasQuotesAndNewlines()
    {
        var service = new SteamLibraryExportService();

        var library = new OwnedGamesResponse(
            76561197960287930L,
            3,
            [
                new Game(
                    1,
                    "Game, One",
                    60,
                    null,
                    0,
                    0,
                    0,
                    0,
                    0),

                new Game(
                    2,
                    "Game \"Two\"",
                    30,
                    null,
                    0,
                    0,
                    0,
                    0,
                    0),

                new Game(
                    3,
                    "Game\r\nThree",
                    15,
                    null,
                    0,
                    0,
                    0,
                    1_700_000_000,
                    0)
            ]);

        var csv = Encoding.UTF8.GetString(
            service.Export(
                library,
                new Dictionary<
                    int,
                    SteamDeckCompatibilityCategory>()));

        Assert.Equal(
            "game,id,hours,last_played,steam_deck\r\n" +
            "\"Game, One\",1,1,,unknown\r\n" +
            "\"Game \"\"Two\"\"\",2,0.5,,unknown\r\n" +
            "\"Game\r\nThree\",3,0.25,2023-11-14T22:13:20Z,unknown\r\n",
            csv);
    }

    [Fact]
    public void Export_WritesSteamDeckCompatibilityStatuses()
    {
        var service = new SteamLibraryExportService();

        var library = new OwnedGamesResponse(
            76561197960287930L,
            5,
            [
                new Game(1, "Verified Game", 0, null, 0, 0, 0, 0, 0),
                new Game(2, "Playable Game", 0, null, 0, 0, 0, 0, 0),
                new Game(3, "Unsupported Game", 0, null, 0, 0, 0, 0, 0),
                new Game(4, "Unknown Game", 0, null, 0, 0, 0, 0, 0),
                new Game(5, "Missing Game", 0, null, 0, 0, 0, 0, 0)
            ]);

        var compatibility =
            new Dictionary<int, SteamDeckCompatibilityCategory>
            {
                [1] = SteamDeckCompatibilityCategory.Verified,
                [2] = SteamDeckCompatibilityCategory.Playable,
                [3] = SteamDeckCompatibilityCategory.Unsupported,
                [4] = SteamDeckCompatibilityCategory.Unknown
            };

        var csv = Encoding.UTF8.GetString(
            service.Export(
                library,
                compatibility));

        Assert.Equal(
            "game,id,hours,last_played,steam_deck\r\n" +
            "Verified Game,1,0,,verified\r\n" +
            "Playable Game,2,0,,playable\r\n" +
            "Unsupported Game,3,0,,unsupported\r\n" +
            "Unknown Game,4,0,,unknown\r\n" +
            "Missing Game,5,0,,unknown\r\n",
            csv);
    }

    [Fact]
    public void Export_OmitsSteamEpochSentinelLastPlayedDate()
    {
        var service = new SteamLibraryExportService();

        var library = new OwnedGamesResponse(
            76561197960287930L,
            1,
            [
                new Game(
                1,
                "Old Game",
                60,
                null,
                0,
                0,
                0,
                86_400,
                0)
            ]);

        var csv = Encoding.UTF8.GetString(
            service.Export(
                library,
                new Dictionary<
                    int,
                    SteamDeckCompatibilityCategory>()));

        Assert.Equal(
            "game,id,hours,last_played,steam_deck\r\n" +
            "Old Game,1,1,,unknown\r\n",
            csv);
    }
}
