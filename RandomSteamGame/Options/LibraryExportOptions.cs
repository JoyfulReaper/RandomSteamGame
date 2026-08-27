/*
 * Random Steam Game
 *
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using System.ComponentModel.DataAnnotations;

namespace RandomSteamGame.Options;

public sealed class LibraryExportOptions
{
    public const string SectionName = "Steam:LibraryExport";

    [Range(1, 32)]
    public int GlobalConcurrency { get; init; } = 2;
}