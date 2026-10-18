using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RandomSteamGame.IrcBot;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var config = builder.Configuration;

// Validate Config
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

var channel = config["Irc:Channel"]
    ?? throw new InvalidOperationException("Irc:Channel missing");

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://randomsteam.kgivler.com/")
};
var randomSteamClient = new RandomSteamClient(httpClient);

var client = new IrcClient(
    server,
    port,
    nick,
    username,
    realName,
    password,
    channel,
    randomSteamClient);

var ct = new CancellationTokenSource();
await client.RunAsync(ct.Token);