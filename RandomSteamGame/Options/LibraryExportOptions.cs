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

    public LibraryExportRateLimitMode RateLimitMode { get; init; } = LibraryExportRateLimitMode.PerIp;

    [Range(1, 43200)]
    public int GlobalCooldownMinutes { get; init; } = 20;

    // Global mode is deliberately limited to one generation, regardless of the PerIp capacity.
    public int EffectiveGlobalConcurrency => RateLimitMode == LibraryExportRateLimitMode.Global ? 1 : GlobalConcurrency;

    public string RateLimitDescription => RateLimitMode == LibraryExportRateLimitMode.Global
        ? $"Exports are shared across all visitors: one at a time, with a {GlobalCooldownMinutes}-minute cooldown starting when an export is accepted."
        : "One export per IP address every 72 hours.";
}
