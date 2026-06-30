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
using System.Runtime.InteropServices.JavaScript;


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
builder.Services.AddScoped<ISteamIdentityReader, BrowserSteamIdentityReader>();
builder.Services.AddScoped<ISteamIdentityWriter, BrowserSteamIdentityWriter>();

// ==========================================
// APPLICATION API CLIENT
// ==========================================
builder.Services.AddScoped<IGameApiClient, GameApiClient>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

if (OperatingSystem.IsBrowser())
{
    await JSHost.ImportAsync("CookieModule", "../js/cookieHelper.js");
}
await builder.Build().RunAsync();