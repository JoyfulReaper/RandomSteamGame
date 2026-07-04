/*
 * Steam Api Client
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.Text.RegularExpressions;

namespace SteamApiClient;

public static partial class SteamVanityUrlHelper
{
    private const string SteamCommunityHost = "steamcommunity.com";
    private const string SteamCommunityHostWithWww = "www.steamcommunity.com";

    [GeneratedRegex("^[a-z0-9_-]{3,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex VanityPattern();

    public static bool TryNormalize(string? input, out string normalizedVanity)
    {
        normalizedVanity = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var decodedInput = Uri.UnescapeDataString(input.Trim()).Trim();
        if (decodedInput.Length == 0)
        {
            return false;
        }

        string candidate;
        if (LooksLikeSteamCommunityUrl(decodedInput))
        {
            if (!TryExtractVanityFromUrl(decodedInput, out candidate))
            {
                return false;
            }
        }
        else
        {
            candidate = decodedInput.Trim('/');
        }

        if (candidate.Length == 0 || candidate.Contains('/'))
        {
            return false;
        }

        candidate = candidate.ToLowerInvariant();
        if (!VanityPattern().IsMatch(candidate))
        {
            return false;
        }

        normalizedVanity = candidate;
        return true;
    }

    public static string Normalize(string input)
    {
        if (TryNormalize(input, out var normalizedVanity))
        {
            return normalizedVanity;
        }

        throw new ArgumentException(
            "Steam vanity URL must resolve to a valid vanity name after decoding and normalization.",
            nameof(input));
    }

    public static string BuildCacheKey(string normalizedVanity)
        => $"vanity_{normalizedVanity}";

    public static string BuildNotFoundCacheKey(string normalizedVanity)
        => $"vanity_not_found_{normalizedVanity}";

    private static bool LooksLikeSteamCommunityUrl(string input)
    {
        return input.Contains("://", StringComparison.Ordinal) ||
               input.StartsWith($"{SteamCommunityHost}/", StringComparison.OrdinalIgnoreCase) ||
               input.StartsWith($"{SteamCommunityHostWithWww}/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractVanityFromUrl(string input, out string vanitySegment)
    {
        vanitySegment = string.Empty;

        var candidateUrl = input.Contains("://", StringComparison.Ordinal)
            ? input
            : $"https://{input}";

        if (!Uri.TryCreate(candidateUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Host, SteamCommunityHost, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, SteamCommunityHostWithWww, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 2 ||
            !string.Equals(segments[0], "id", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        vanitySegment = segments[1].Trim('/');
        return vanitySegment.Length > 0;
    }
}
