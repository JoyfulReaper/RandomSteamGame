using RandomSteamGame.Shared.Contracts;
using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityReader : ISteamIdentityReader
{
    private readonly IHttpContextAccessor _http;

    public ServerSteamIdentityReader(IHttpContextAccessor http)
    {
        _http = http;
    }

    public ValueTask<SteamIdentity> GetIdentityAsync()
    {
        var steamId = _http.HttpContext?.Request.Cookies["SteamId"];
        var vanityUrl = _http.HttpContext?.Request.Cookies["VanityUrl"];
        var unplayedOnlyCookie = _http.HttpContext?.Request.Cookies["UnplayedOnly"];
        var unplayedOnly = bool.TryParse(unplayedOnlyCookie, out var parsedUnplayedOnly) && parsedUnplayedOnly;

        return ValueTask.FromResult(new SteamIdentity(steamId, vanityUrl, unplayedOnly));
    }

    public ValueTask<string?> GetSteamIdAsync()
    {
        var value = _http.HttpContext?.Request.Cookies["SteamId"];
        return ValueTask.FromResult(value);
    }

    public ValueTask<string?> GetVanityUrlAsync()
    {
        var value = _http.HttpContext?.Request.Cookies["VanityUrl"];
        return ValueTask.FromResult(value);
    }
}
