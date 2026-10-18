using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Json;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var config = builder.Configuration;

// Validate Config
var username = config["Matrix:Username"]
    ?? throw new InvalidOperationException("Matric:Username missing");


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

Console.WriteLine(response.StatusCode);
Console.WriteLine(await response.Content.ReadAsStringAsync());


