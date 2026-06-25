/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Services.Interfaces;

public interface IHtmlSanitizerService
{
    public string Sanitize(string? html);
}
