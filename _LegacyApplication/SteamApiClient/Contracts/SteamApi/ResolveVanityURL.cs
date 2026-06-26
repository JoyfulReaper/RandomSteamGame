namespace SteamApiClient.Contracts.SteamApi;

public class ResolveVanityUrlResponse
{
    public ResolvedVanityUrl Response { get; set; } = default!;
}

public class ResolvedVanityUrl
{
    public string SteamId { get; set; } = string.Empty;
    public int Success { get; set; }
}