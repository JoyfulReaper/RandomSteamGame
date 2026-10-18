using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RandomSteamGame.MatrixBot.Matrix;
using System.Net.Http.Headers;
using System.Net.Http.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var config = builder.Configuration;

// Validate Config
var username = config["Matrix:Username"]
    ?? throw new InvalidOperationException("Matrix:Username missing");


var password = config["Matrix:Password"]
    ?? throw new InvalidOperationException("Matrix:Password missing");

var loginRequest = new
{
    type = "m.login.password",
    identifier = new
    {
        type = "m.id.user",
        user = username
    },
    password = password
};

using var client = new HttpClient();
var response = await client.PostAsJsonAsync("https://matrix.kgivler.com/_matrix/client/v3/login", loginRequest);
response.EnsureSuccessStatusCode();
var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>()
    ?? throw new InvalidOperationException("Failed to login");

if (loginResponse.AccessToken is null)
{
    throw new InvalidOperationException("Access token was missing from the server response.");
}


client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse?.AccessToken);
var syncResponse = await client.GetFromJsonAsync<SyncResponse>("https://matrix.kgivler.com/_matrix/client/v3/sync?timeout=30000");

Console.WriteLine($"Logged in as {loginResponse?.UserId}");
Console.WriteLine($"Next Batch: {syncResponse?.NextBatch}");