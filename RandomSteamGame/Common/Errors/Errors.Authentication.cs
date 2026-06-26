/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;

namespace RandomSteamGame.Common.Errors;

public static partial class Errors
{
    public static class Authentication
    {
        public static Error InvalidCredentials => Error.Validation(
            code: "Auth.InvalidCred",
            description: "Invalid Credentials.");

        public static Error InvalidToken => Error.Failure(
            code: "Auth.InvalidToken",
            description: "Invalid Token.");
    }
}