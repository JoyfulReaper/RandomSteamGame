/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Microsoft.AspNetCore.Mvc.Infrastructure;
using RandomSteamGameBlazor.Server;
using RandomSteamGameBlazor.Server.Common.Errors;
using RandomSteamGameBlazor.Server.Services;
using SteamApiClient;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    builder.Services.AddScoped<ISteamService, SteamService>();
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddRandomSteamGame(builder.Configuration);
    builder.Services.AddSteamApiClient(builder.Configuration);

    builder.Services.AddSingleton<ProblemDetailsFactory, RandomSteamProblemDetailsFactory>();
}

var app = builder.Build();
{
    if (app.Environment.IsDevelopment())
    {
        app.UseWebAssemblyDebugging();
    }
    else
    {
        app.UseHsts();
        app.UseExceptionHandler("/error");
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseAuthorization();

    app.MapRazorPages();
    app.MapControllers();
    app.MapFallbackToFile("index.html");

    app.Run();
}