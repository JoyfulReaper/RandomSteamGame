/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Shared.Contracts;

public sealed class ApiProblem
{
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
}