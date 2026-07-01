/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Client.Pages;

using System.ComponentModel.DataAnnotations;

public class HomeModel : IValidatableObject
{
    public string? SteamId { get; set; } = null;
    public string? VanityUrl { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasSteamId = !string.IsNullOrWhiteSpace(SteamId);
        var hasVanityUrl = !string.IsNullOrWhiteSpace(VanityUrl);

        if (!hasSteamId && !hasVanityUrl)
        {
            yield return new ValidationResult(
                "Enter a Steam ID or vanity URL.",
                [nameof(SteamId), nameof(VanityUrl)]);
        }

        if (hasSteamId && hasVanityUrl)
        {
            yield return new ValidationResult(
                "Enter either a Steam ID or vanity URL, not both.",
                [nameof(SteamId), nameof(VanityUrl)]);
        }

        if (hasSteamId && (SteamId!.Length != 17 || !SteamId.All(char.IsDigit)))
        {
            yield return new ValidationResult(
                "Steam ID must be exactly 17 digits.",
                [nameof(SteamId)]);
        }
    }
}
