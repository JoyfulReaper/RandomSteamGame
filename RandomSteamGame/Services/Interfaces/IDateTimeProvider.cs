/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Services.Interfaces;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}