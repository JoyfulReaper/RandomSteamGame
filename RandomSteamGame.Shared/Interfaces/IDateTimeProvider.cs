/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Shared.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
