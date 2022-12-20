using MonkeyCache.SQLite;
using System.Text.Json;

namespace RandomSteamGame;

public static class HttpClientExtensions
{
    public static async Task<T> MonkeyCacheGetAsync<T>(this HttpClient client, string url, int days = 7, bool forceRefresh = false)
    {
        string json = string.Empty;

        if (!forceRefresh && !Barrel.Current.IsExpired(key: url))
        {
            json = Barrel.Current.Get<string>(url);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                json = await client.GetStringAsync(url);
                Barrel.Current.Add(url, json, TimeSpan.FromDays(days));
            }

            var output =
                JsonSerializer.Deserialize<T>(json);

            return output!;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to get information from server {ex}");
            throw;
        }
    }

    public static async Task<string> MonkeyCacheGetJsonStringAsync(this HttpClient client, string url, int days = 7, bool forceRefresh = false)
    {
        string jsonString = string.Empty;

        if (!forceRefresh && !Barrel.Current.IsExpired(key: url))
        {
            jsonString = Barrel.Current.Get<string>(url);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(jsonString))
            {
                jsonString = await client.GetStringAsync(url);
                Barrel.Current.Add(url, jsonString, TimeSpan.FromDays(days));
            }

            return jsonString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unable to get information from server {ex}");
            throw;
        }
    }
}
