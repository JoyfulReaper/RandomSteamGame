/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace SteamApiClient.Exceptions;

public class VanityResolutionException : Exception
{
    public VanityResolutionException()
    {
    }

    public VanityResolutionException(string message)
        : base(message)
    {
    }

    public VanityResolutionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}