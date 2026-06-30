/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Client.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using RandomSteamGame.Shared.Services;


var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ==========================================
// CORE NETWORKING
// ==========================================
builder.Services.AddHttpClient<BackendApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

// ==========================================
// CORE SECURITY & STORAGE FOUNDATIONS
// ==========================================
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped<BrowserSteamIdentityStore>();
builder.Services.AddScoped<IBrowserSteamIdentityStore>(sp => sp.GetRequiredService<BrowserSteamIdentityStore>());
builder.Services.AddScoped<ISteamIdentityReader>(sp => sp.GetRequiredService<BrowserSteamIdentityStore>());
builder.Services.AddScoped<ISteamIdentityWriter>(sp => sp.GetRequiredService<BrowserSteamIdentityStore>());

// ==========================================
// APPLICATION API CLIENT
// ==========================================
builder.Services.AddScoped<IGameApiClient, GameApiClient>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

await builder.Build().RunAsync();
