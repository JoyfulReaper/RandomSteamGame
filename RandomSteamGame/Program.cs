/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.JRData;
using RandomSteamGame.Components;
using RandomSteamGame.Extensions;

var builder = WebApplication.CreateBuilder(args);

var schema = @"
            CREATE TABLE IF NOT EXISTS Visitors (
                IpAddress TEXT PRIMARY KEY,
                Hits INTEGER DEFAULT 1,
                LastSeen TEXT
            );";
var connectionString = SqliteHelper.InitializeSqlite("kgivler_com.db", schema);

builder.Services.AddApplicationServices(builder.Configuration, builder.Environment, connectionString);
var app = builder.Build();

app.ConfigurePipeline(app.Environment);

// ==========================================
// ENDPOINTS & ROUTING
// ==========================================
app.MapStaticAssets();
app.MapControllers();

// For testing custom 500 page
//app.MapGet("/trigger-500", () =>
//{
//    throw new Exception("You wanted a 500? ok!");
//});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();