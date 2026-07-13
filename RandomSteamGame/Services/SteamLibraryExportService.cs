using System.Globalization;
using System.Text;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Services;

public sealed class SteamLibraryExportService : ISteamLibraryExportService
{
    private const string CsvNewLine = "\r\n";
    private const string UnknownSteamDeckStatus = "unknown";

    public byte[] Export(OwnedGamesResponse library)
    {
        var builder = new StringBuilder();
        builder.Append("game,id,hours,last_played,steam_deck");
        builder.Append(CsvNewLine);

        foreach (var game in library.Games)
        {
            builder.Append(Escape(game.Name ?? string.Empty));
            builder.Append(',');
            builder.Append(game.AppId.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(FormatHours(game.PlaytimeForever));
            builder.Append(',');
            builder.Append(FormatLastPlayed(game.RTimeLastPlayed));
            builder.Append(',');
            builder.Append(UnknownSteamDeckStatus);
            builder.Append(CsvNewLine);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string FormatHours(int playtimeMinutes)
    {
        var hours = playtimeMinutes / 60m;
        return hours.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatLastPlayed(long rTimeLastPlayed)
    {
        if (rTimeLastPlayed <= 0)
        {
            return string.Empty;
        }

        return DateTimeOffset
            .FromUnixTimeSeconds(rTimeLastPlayed)
            .UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static string Escape(string value)
    {
        var requiresQuotes = value.Contains(',') ||
                             value.Contains('"') ||
                             value.Contains('\r') ||
                             value.Contains('\n');

        if (!requiresQuotes)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
