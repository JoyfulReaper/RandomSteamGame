using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Globalization;
using System.Text;
using SteamDeckCompatibilityCategory = SteamApiClient.Contracts.SteamApi.SteamDeckCompatibilityCategory;

namespace RandomSteamGame.Services;

public sealed class SteamLibraryExportService : ISteamLibraryExportService
{
    private const string CsvNewLine = "\r\n";

    public byte[] Export(
        OwnedGamesResponse library,
        IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>
        steamDeckCompatibility)
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
            builder.Append(FormatSteamDeckStatus(game.AppId, steamDeckCompatibility));
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
        if (rTimeLastPlayed <= 86_400) // 86_400 is exactly Jan 2, 1970 UTC
        {
            return string.Empty;
        }

        return DateTimeOffset
            .FromUnixTimeSeconds(rTimeLastPlayed)
            .UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
    }

    private static string FormatSteamDeckStatus(
        int appId,
        IReadOnlyDictionary<int, SteamDeckCompatibilityCategory>
        steamDeckCompatibility)
    {
        if (!steamDeckCompatibility.TryGetValue(appId, out var category))
        {
            return "unknown";
        }

        return category switch
        {
            SteamDeckCompatibilityCategory.Unsupported => "unsupported",
            SteamDeckCompatibilityCategory.Playable => "playable",
            SteamDeckCompatibilityCategory.Verified => "verified",
            _ => "unknown"
        };
    }

    private static string Escape(string value)
    {
        value = ProtectSpreadsheetFormula(value);

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

    private static string ProtectSpreadsheetFormula(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] switch
        {
            '=' or '+' or '-' or '@' => $"'{value}",
            _ => value
        };
    }
}
