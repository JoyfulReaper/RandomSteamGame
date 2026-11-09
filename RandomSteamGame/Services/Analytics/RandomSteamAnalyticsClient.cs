
using Microsoft.Extensions.Options;
using RandomSteamGame.Options;

namespace RandomSteamGame.Services.Analytics;

public sealed class RandomSteamAnalyticsClient(
    HttpClient httpClient,
    IOptions<RandomSteamAnalyticsOptions> options)
{
    private readonly RandomSteamAnalyticsOptions _options = options.Value;

    public async Task<RandomSteamOverview> GetOverviewAsync(
        int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (days is < 1 or > 90)
            throw new ArgumentOutOfRangeException(nameof(days));

        if (!_options.Enabled)
            throw new InvalidOperationException("RandomSteam analytics is not enabled.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/randomsteam/analytics/overview?days={days}");

        request.Headers.Add(
            "X-RandomSteam-Analytics-Key",
            _options.ApiKey);

        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RandomSteamOverview>(
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The analytics API returned an empty response.");
    }
}
