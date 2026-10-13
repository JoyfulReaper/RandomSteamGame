using RandomSteamGame.Shared.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace RandomSteamGame.IrcBot;

public sealed class RandomSteamClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RandomSteamClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GameDetails?> GetRandomGameAsync(
        string identifier,
        CancellationToken ct)
    {
        string requestUri = $"api/steam/random-game/details?userId={identifier}";
        try
        {
            var gameDetails = await _httpClient.GetFromJsonAsync<GameDetails>(requestUri, JsonOptions, ct);
            return gameDetails;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[HTTP Error]: {ex.Message}");
            throw;
        }
    }
}