using MediatR;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using RandomSteamGameBlazor.Server;
using RandomSteamGameBlazor.Server.Common.Behaviors;
using RandomSteamGameBlazor.Server.Common.Errors;
using SteamApiClient;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    builder.Services.AddRandomSteamGame(builder.Configuration);
    builder.Services.AddSteamApiClient(builder.Configuration);
    
    builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
    builder.Services.AddSingleton<ProblemDetailsFactory, RandomSteamProblemDetailsFactory>();
    builder.Services.AddScoped(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));
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