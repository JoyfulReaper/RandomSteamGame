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

        public static Error VanityResolutionFailed => Error.NotFound(
            code: "Steam.VanityResolutionFailed",
            description: "Failed to resolve vanity URL.");

        public static Error VanityResolutonFailed => VanityResolutionFailed;

        public static Error EmptyLibrary => Error.NotFound(
            code: "Steam.EmptyLibrary",
            description: "Steam Library does not contain any games.");

        public static Error SteamApiFailed => Error.Failure(
            code: "Steam.ApiFailed",
            description: "Steam API failed to return data. Is your Steam profile public?");

        public static Error SteamApiSuccessButCouldntGetAppData => Error.Failure(
            code: "Steam.ApiSuccessButCouldntGetAppData",
            description: "Steam API returned success but we were unable to get app data for any supplied AppIds after the maximum number of retries.");
    }
}
