using MediatR;
using RandomSteamGameBlazor.Server;
using SteamApiClient;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
{
    builder.Services.AddControllersWithViews();
    builder.Services.AddRazorPages();

    builder.Services.AddRandomSteamGame(builder.Configuration);
    builder.Services.AddSteamApiClient(builder.Configuration);
    builder.Services.AddMediatR(Assembly.GetExecutingAssembly());
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

    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseRouting();

    app.MapRazorPages();
    app.MapControllers();
    app.MapFallbackToFile("index.html");

    app.Run();
}