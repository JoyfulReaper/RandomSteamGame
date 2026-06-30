/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Shared.Contracts;

public sealed record GameDetails
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
    public string Description { get; init; } = default!;
    public string HeaderImage { get; init; } = default!;
}