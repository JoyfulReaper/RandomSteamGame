using RandomSteamGame.Services;
using RandomSteamGame.Shared.Contracts;
using System.Text;

namespace RandomSteamGame.Tests;

public class SteamLibraryExportServiceTests
{
    [Fact]
    public void Export_EscapesNamesWithCommasQuotesAndNewlines()
    {
        var service = new SteamLibraryExportService();
        var library = new OwnedGamesResponse(76561197960287930L, 3, [
            new Game(1, "Game, One", 60, null, 0, 0, 0, 0, 0),
            new Game(2, "Game \"Two\"", 30, null, 0, 0, 0, 0, 0),
            new Game(3, "Game\r\nThree", 15, null, 0, 0, 0, 1_700_000_000, 0)
        ]);

        var csv = Encoding.UTF8.GetString(service.Export(library));

        Assert.Equal(
            "game,id,hours,last_played,steam_deck\r\n" +
            "\"Game, One\",1,1,,unknown\r\n" +
            "\"Game \"\"Two\"\"\",2,0.5,,unknown\r\n" +
            "\"Game\r\nThree\",3,0.25,2023-11-14T22:13:20Z,unknown\r\n",
            csv);
    }
}
