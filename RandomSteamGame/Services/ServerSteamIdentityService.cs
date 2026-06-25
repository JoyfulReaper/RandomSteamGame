/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */


using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityService : ISteamIdentityService
{
    public Task<long?> GetSteamIdAsync() =>
        Task.FromResult<long?>(null);

    public Task SetIdentityAsync(long steamId, string? vanityUrl) =>
        Task.CompletedTask;

    public Task LogoutAsync() =>
        Task.CompletedTask;
}