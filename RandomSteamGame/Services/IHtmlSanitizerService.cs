/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Services;

public interface IHtmlSanitizerService
{
    public string Sanitize(string? html);
}
