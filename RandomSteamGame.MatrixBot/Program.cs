using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RandomSteamGame.MatrixBot.MatrixClient;

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

// Create the http client for Matrix
using var httpClient = new HttpClient
{
    BaseAddress = new Uri(homeserver)
};

var matrixClient = new MatrixClient(httpClient, username, password);
await matrixClient.LoginAsync();
var identity = await matrixClient.WhoAmIAsync();


Console.WriteLine($"Authenticated as {identity?.UserId}");

var first = await matrixClient.SyncAsync(null);
Console.WriteLine($"First sync: {first?.NextBatch}");

var second = await matrixClient.SyncAsync(first?.NextBatch);
Console.WriteLine($"Second sync: {second?.NextBatch}");