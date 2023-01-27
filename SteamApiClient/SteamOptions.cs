namespace SteamApiClient;

public class SteamOptions
{
    public string ApiKey { get; set; } = default!;
    public string ConnectionString { get; set; } = default!;
    public string CacheSchema { get; set; } = default!;
    public string CacheTable { get; set; } = default!;
}
