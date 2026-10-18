using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RandomSteamGame.MatrixBot.Bot;
using RandomSteamGame.MatrixBot.MatrixClient;
using RandomSteamGame.SteamClient;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var config = builder.Configuration;

// Validate Config
var username = config["Matrix:Username"]
    ?? throw new InvalidOperationException("Matrix:Username missing");

var password = config["Matrix:Password"]
    ?? throw new InvalidOperationException("Matrix:Password missing");

var homeserver = config["Matrix:Homeserver"]
    ?? throw new InvalidOperationException("Matrix:Homeserver missing");

var roomId = config["Matrix:RoomId"]
    ?? throw new InvalidOperationException("Matrix:RoomId missing");

// Create the http client for Matrix
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(homeserver)
};

// Create http client for RandomSteam
using var steamHttpClient = new HttpClient
{
    BaseAddress = new Uri("https://randomsteam.kgivler.com/")
};
var steamClient = new RandomSteamClient(steamHttpClient);

// Start the bot
var bot = new MatrixBot(new MatrixClient(httpClient, username, password), roomId, steamClient);
await bot.RunAsync();