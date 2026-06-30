/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Components;
using RandomSteamGame.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationServices(builder.Configuration, builder.Environment);
var app = builder.Build();

app.ConfigurePipeline(app.Environment);

// ==========================================
// ENDPOINTS & ROUTING
// ==========================================
app.MapStaticAssets();
app.MapControllers();

//For testing custom 500 page
//app.MapGet("/trigger-500", () =>
//{
//    throw new Exception("You wanted a 500? ok!");
//});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();