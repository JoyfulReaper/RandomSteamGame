using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RandomSteamGame.IrcBot;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var config = builder.Configuration;

var server = config["Irc:Server"]
    ?? throw new InvalidOperationException("Irc:Server missing");

var port = config.GetValue<int>("Irc:Port");

var nick = config["Irc:Nick"]
    ?? throw new InvalidOperationException("Irc:Nick missing");

var username = config["Irc:Username"]
    ?? throw new InvalidOperationException("Irc:Username missing");

var realName = config["Irc:RealName"]
    ?? throw new InvalidOperationException("Irc:RealName missing");

var password = config["Irc:Password"]
    ?? throw new InvalidOperationException("Irc:Password missing");

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://randomsteam.kgivler.com/")
};

//var randomSteamClient = new RandomSteamClient(httpClient);
//var game = await randomSteamClient.GetRandomGameAsync(
//    "76561197988408972",
//    CancellationToken.None);

//Console.WriteLine($"{game?.Name} - {game?.Id}");

var client = new IrcClient(
    server,
    port,
    nick,
    username,
    realName,
    password);

var ct = new CancellationTokenSource();
await client.RunAsync(ct.Token);