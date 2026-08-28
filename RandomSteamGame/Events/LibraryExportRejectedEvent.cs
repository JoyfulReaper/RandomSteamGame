/*
 * Random Steam Game
 *
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

namespace RandomSteamGame.Events;

public sealed record LibraryExportRejectedEvent(
    string? VisitorId,
    string? Provider,
    string Reason,
    long? RetryAfterSeconds,
    string? CommitSha);