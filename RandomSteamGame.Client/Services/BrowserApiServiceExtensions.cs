namespace RandomSteamGame.Client.Services;

public static class BrowserApiServiceExtensions
{
    public static IServiceCollection AddRandomSteamBrowserApiClient(this IServiceCollection services, string baseAddress)
    {
        services.AddHttpClient<IRandomSteamApiClient, RandomSteamApiClient>(client =>
            client.BaseAddress = new Uri(baseAddress));
        return services;
    }
}
