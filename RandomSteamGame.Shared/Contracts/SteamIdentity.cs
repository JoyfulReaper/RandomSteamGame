/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


namespace RandomSteamGame.Shared.Contracts;

public sealed record SteamIdentity(
    string? SteamId,
    string? VanityUrl,
    bool UnplayedOnly);
