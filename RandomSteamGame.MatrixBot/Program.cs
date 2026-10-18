
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RandomSteamGame.MatrixBot.Bot;
using RandomSteamGame.MatrixBot.MatrixClient;
using RandomSteamGame.SteamClient;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var config = builder.Configuration;

// Validate configuration
var username = config["Matrix:Username"]
    ?? throw new InvalidOperationException("Matrix:Username missing");

var password = config["Matrix:Password"];
if (string.IsNullOrWhiteSpace(password))
    throw new InvalidOperationException("Matrix:Password missing");

var homeserver = config["Matrix:Homeserver"]
    ?? throw new InvalidOperationException("Matrix:Homeserver missing");

var roomId = config["Matrix:RoomId"]
    ?? throw new InvalidOperationException("Matrix:RoomId missing");

// Create HTTP client for Matrix
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(homeserver)
};

// Create HTTP client for Random Steam Game
using var steamHttpClient = new HttpClient
{
    BaseAddress = new Uri("https://randomsteam.kgivler.com/")
};

var steamClient = new RandomSteamClient(steamHttpClient);

// Build and start the host
using var host = builder.Build();

var bot = new MatrixBot(
    new MatrixClient(httpClient, username, password),
    roomId,
    steamClient);

await host.StartAsync();

try
{
    await bot.RunAsync(host.Services
        .GetRequiredService<IHostApplicationLifetime>()
        .ApplicationStopping);
}
catch (OperationCanceledException) when (host.Services.GetRequiredService<IHostApplicationLifetime>()
        .ApplicationStopping.IsCancellationRequested)
{
    Console.WriteLine("Bot shutting down.");
}
finally
{
    await host.StopAsync();
}
