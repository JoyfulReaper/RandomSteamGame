using RandomSteamGame.Shared.Interfaces;

namespace RandomSteamGame.Services;

public class ServerSteamIdentityReader : ISteamIdentityReader
{
    private readonly IHttpContextAccessor _http;

    public ServerSteamIdentityReader(IHttpContextAccessor http)
    {
        _http = http;
    }

    public ValueTask<long?> GetSteamIdAsync()
    {
        var value = _http.HttpContext?.Request.Cookies["SteamId"];
        return ValueTask.FromResult(long.TryParse(value, out var id) ? id : (long?)null);
    }

    public ValueTask<string?> GetVanityUrlAsync()
    {
        var value = _http.HttpContext?.Request.Cookies["VanityUrl"];
        return ValueTask.FromResult(value);
    }
}