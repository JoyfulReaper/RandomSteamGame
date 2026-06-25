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


var builder = WebAssemblyHostBuilder.CreateDefault(args);

// ==========================================
// CORE NETWORKING
// ==========================================
// Configures the primary HttpClient pointing directly back to your server's domain
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Named/Factory HttpClient support for client-side web requests
builder.Services.AddHttpClient();

// ==========================================
// CORE SECURITY & STORAGE FOUNDATIONS
// ==========================================
// These are built-in framework or library types that exist immediately once packages are added
builder.Services.AddAuthorizationCore();
builder.Services.AddLocalStorage();

// ==========================================
// APPLICATION API CLIENT
// ==========================================
builder.Services.AddScoped<IGameApiClient, GameApiClient>();
builder.Services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

await builder.Build().RunAsync();