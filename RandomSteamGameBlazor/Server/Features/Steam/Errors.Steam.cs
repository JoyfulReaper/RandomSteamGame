using ErrorOr;

namespace RandomSteamGameBlazor.Server.Features.Steam;

public static partial class Errors
{
    public static class Steam
    {
        public static Error VanityResolutonFailed => Error.NotFound(
            code: "Steam.VanityResolutonFailed",
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
    }
}
