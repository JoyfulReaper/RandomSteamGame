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

app.MapMethods("/", [HttpMethods.Head], (HttpContext context) =>
{
    context.Response.ContentType = "text/html; charset=utf-8";
    return Results.Empty;
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();
