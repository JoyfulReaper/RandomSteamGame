using Blazored.LocalStorage;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace RandomSteamGameBlazor.Client.Features.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomAuthenticationStateProvider(ILocalStorageService localStorage,
        HttpClient http,
        IHttpContextAccessor httpContextAccessor)
    {
        _localStorage = localStorage;
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        string token = await _localStorage.GetItemAsStringAsync("token");

        var identity = new ClaimsIdentity();
        _http.DefaultRequestHeaders.Authorization = null;

        if (!string.IsNullOrEmpty(token))
        {
            var claims = ParseClaimsFromJwt(token).ToList();
            var expiration = claims.Where(c => c.Type == "exp").FirstOrDefault();
            if (expiration != null)
            {
                var expirationDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiration.Value));
                var now = DateTime.UtcNow;
                if (expirationDate < now)
                {
                    await _localStorage.RemoveItemAsync("token");
                    token = string.Empty;
                    claims = new List<Claim>();
                }
            }

            identity = new ClaimsIdentity(claims, "jwt", "name", null);


            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.Replace("\"", ""));
        }

        var user = new ClaimsPrincipal(identity);
        var state = new AuthenticationState(user);

        NotifyAuthenticationStateChanged(Task.FromResult(state));

        return state;
    }

    public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);
        return keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()));
    }

    private static byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}