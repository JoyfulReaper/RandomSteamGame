/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Shared.Interfaces;
using RandomSteamGame.Shared.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ==========================================
// CORE NETWORKING
// ==========================================
builder.Services.AddHttpClient<RandomSteamApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
});

builder.Services.AddScoped<BrowserSteamIdentityStore>();
builder.Services.AddScoped<IBrowserSteamIdentityStore>(sp => sp.GetRequiredService<BrowserSteamIdentityStore>());
builder.Services.AddScoped<ISteamIdentityReader>(sp => sp.GetRequiredService<BrowserSteamIdentityStore>());
builder.Services.AddScoped<ISteamIdentityWriter>(sp => sp.GetRequiredService<BrowserSteamIdentityStore>());

// ==========================================
// APPLICATION API CLIENT
// ==========================================
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

await builder.Build().RunAsync();
