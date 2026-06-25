/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Mythetech.LocalStorage;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Client.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;


var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ==========================================
// CORE NETWORKING
// ==========================================
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Named/Factory HttpClient support for client-side web requests
builder.Services.AddHttpClient();

// ==========================================
// CORE SECURITY & STORAGE FOUNDATIONS
// ==========================================
builder.Services.AddAuthorizationCore();
builder.Services.AddLocalStorage();
builder.Services.AddScoped<ISteamIdentityService, ManualSteamIdentityService>();

// ==========================================
// APPLICATION API CLIENT
// ==========================================
builder.Services.AddScoped<IGameApiClient, GameApiClient>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

await builder.Build().RunAsync();