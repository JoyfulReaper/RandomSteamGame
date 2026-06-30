using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityReader : ISteamIdentityReader
{
    private readonly IHttpContextAccessor _http;

    public ServerSteamIdentityReader(IHttpContextAccessor http)
    {
        _http = http;
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