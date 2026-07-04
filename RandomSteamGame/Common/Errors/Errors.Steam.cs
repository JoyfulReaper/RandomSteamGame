using ErrorOr;

namespace RandomSteamGame.Common.Errors;

public static partial class Errors
{
    public static class Steam
    {
        public static Error UnsupportedProvider(string provider) => Error.Validation(
            code: "Steam.UnsupportedProvider",
            description: $"Provider '{provider}' is not supported.");

        public static Error IdentifierRequired => Error.Validation(
            code: "Steam.IdentifierRequired",
            description: "A Steam ID or vanity URL is required.");

        public static Error AmbiguousIdentifier => Error.Validation(
            code: "Steam.AmbiguousIdentifier",
            description: "Provide either a Steam ID or vanity URL, not both.");

        public static Error InvalidSteamId => Error.Validation(
            code: "Steam.InvalidSteamId",
            description: "Steam ID must be exactly 17 digits.");

        public static Error InvalidVanityUrl => Error.Validation(
            code: "Steam.InvalidVanityUrl",
            description: "Steam vanity URL must decode to a valid Steam vanity name or Steam community profile URL.");

        public static Error VanityResolutionFailed => Error.NotFound(
            code: "Steam.VanityResolutionFailed",
            description: "Failed to resolve vanity URL.");

        public static Error EmptyLibrary => Error.NotFound(
            code: "Steam.EmptyLibrary",
            description: "Steam Library does not contain any games.");

        public static Error SteamApiFailed => Error.Failure(
            code: "Steam.ApiFailed",
            description: "Steam API failed to return data. Is your Steam profile public?");

        public static Error SteamApiSuccessButCouldntGetAppData => Error.Failure(
            code: "Steam.ApiSuccessButCouldntGetAppData",
            description: "Steam API returned success but we were unable to get app data for any supplied AppIds after the maximum number of retries.");

        public static Error NoSelectableGamesAfterExclusions => Error.NotFound(
            code: "Steam.NoSelectableGamesAfterExclusions",
            description: "Every game in this library is currently excluded. Clear your blocked games and try again.");
    }
}
