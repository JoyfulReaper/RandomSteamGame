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

var dataProtectionSettings = app.Services.GetRequiredService<DataProtectionSettings>();
app.Logger.LogInformation(
    "Data Protection configured. ApplicationName={ApplicationName}, KeysPath={KeysPath}",
    dataProtectionSettings.ApplicationName,
    dataProtectionSettings.KeysPath);

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

if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/throw", () =>
    {
        throw new InvalidOperationException("Intentional test exception for 500 page.");
    });

    app.MapGet("/dev/500", () => Results.StatusCode(StatusCodes.Status500InternalServerError));
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(RandomSteamGame.Client._Imports).Assembly);

app.Run();
