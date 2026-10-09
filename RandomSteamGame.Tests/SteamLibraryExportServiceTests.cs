using RandomSteamGame.Services;
using RandomSteamGame.Shared.Contracts;
using System.Text;
using System.Globalization;
using SteamDeckCompatibilityCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Tests;

public class SteamLibraryExportServiceTests
{
    private const string CsvHeader = "game,id,hours,hours_2_weeks,hours_windows,hours_mac,hours_linux,last_played,steam_deck,steam_store_url\r\n";

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+SUM(A1:A2)", "'+SUM(A1:A2)")]
    [InlineData("-1+2", "'-1+2")]
    [InlineData("@SUM(A1:A2)", "'@SUM(A1:A2)")]
    [InlineData("Normal Game", "Normal Game")]
    public void Export_ProtectsGameNamesFromSpreadsheetFormulaInjection(
    string gameName,
    string expectedName)
    {
        var service = new SteamLibraryExportService();

        var library = new OwnedGamesResponse(
            76561197960287930L,
            1,
            [
                new Game(
                1,
                gameName,
                0,
                null,
                0,
                0,
                0,
                0,
                0)
            ]);

        var csv = Encoding.UTF8.GetString(
            service.Export(
                library,
                new Dictionary<int, SteamDeckCompatibilityCategory>()));

        Assert.Equal(
            CsvHeader +
            $"{expectedName},1,0,0,0,0,0,,unknown,https://store.steampowered.com/app/1/\r\n",
            csv);
    }

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
            CsvHeader +
            "\"Game, One\",1,1,0,0,0,0,,unknown,https://store.steampowered.com/app/1/\r\n" +
            "\"Game \"\"Two\"\"\",2,0.5,0,0,0,0,,unknown,https://store.steampowered.com/app/2/\r\n" +
            "\"Game\r\nThree\",3,0.25,0,0,0,0,2023-11-14T22:13:20Z,unknown,https://store.steampowered.com/app/3/\r\n",
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
            CsvHeader +
            "Verified Game,1,0,0,0,0,0,,verified,https://store.steampowered.com/app/1/\r\n" +
            "Playable Game,2,0,0,0,0,0,,playable,https://store.steampowered.com/app/2/\r\n" +
            "Unsupported Game,3,0,0,0,0,0,,unsupported,https://store.steampowered.com/app/3/\r\n" +
            "Unknown Game,4,0,0,0,0,0,,unknown,https://store.steampowered.com/app/4/\r\n" +
            "Missing Game,5,0,0,0,0,0,,unknown,https://store.steampowered.com/app/5/\r\n",
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
            CsvHeader +
            "Old Game,1,1,0,0,0,0,,unknown,https://store.steampowered.com/app/1/\r\n",
            csv);
    }

    [Fact]
    public void Export_WritesDistinctOwnedLibraryPlaytimesUsingInvariantHours()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var library = new OwnedGamesResponse(76561197960287930L, 1,
                [new Game(620, "Portal 2", 245, null, 125, 45, 75, 1_700_000_000, 35)]);

            var csv = Encoding.UTF8.GetString(new SteamLibraryExportService().Export(library,
                new Dictionary<int, SteamDeckCompatibilityCategory> { [620] = SteamDeckCompatibilityCategory.Verified }));

            Assert.Equal(CsvHeader + "Portal 2,620,4.08,0.58,2.08,0.75,1.25,2023-11-14T22:13:20Z,verified,https://store.steampowered.com/app/620/\r\n", csv);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "0.02")]
    [InlineData(30, "0.5")]
    [InlineData(60, "1")]
    [InlineData(61, "1.02")]
    [InlineData(90, "1.5")]
    public void Export_AllPlaytimeColumnsUseTheSameHoursFormatting(int minutes, string hours)
    {
        var library = new OwnedGamesResponse(76561197960287930L, 1,
            [new Game(400, "Portal", minutes, null, minutes, minutes, minutes, 0, minutes)]);
        var csv = Encoding.UTF8.GetString(new SteamLibraryExportService().Export(library,
            new Dictionary<int, SteamDeckCompatibilityCategory>()));

        Assert.Equal(CsvHeader + $"Portal,400,{hours},{hours},{hours},{hours},{hours},,unknown,https://store.steampowered.com/app/400/\r\n", csv);
    }

    [Theory]
    [InlineData("Été 日本語 🎮", "Été 日本語 🎮")]
    [InlineData("Été, \"日本語\"\n🎮", "\"Été, \"\"日本語\"\"\n🎮\"")]
    [InlineData("=HYPERLINK(\"https://example.com\",\"日本語\")", "\"'=HYPERLINK(\"\"https://example.com\"\",\"\"日本語\"\")\"")]
    public void Export_PreservesUtf8NamesAndFormulaProtectionWhenEscaping(string name, string escapedName)
    {
        var library = new OwnedGamesResponse(76561197960287930L, 1,
            [new Game(400, name, 0, null, 0, 0, 0, 0, 0)]);
        var csv = Encoding.UTF8.GetString(new SteamLibraryExportService().Export(library,
            new Dictionary<int, SteamDeckCompatibilityCategory>()));

        Assert.Equal(CsvHeader + $"{escapedName},400,0,0,0,0,0,,unknown,https://store.steampowered.com/app/400/\r\n", csv);
    }
}
