using Blazored.LocalStorage;
using Microsoft.AspNetCore.Http;
using RandomSteamGameBlazor.Client.Common;
using RandomSteamGameBlazor.Client.Common.Services;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace RandomSteamGameBlazor.Client.Features.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly HttpClient _http;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CustomAuthenticationStateProvider(ILocalStorageService localStorage,
        HttpClient http,
        IDateTimeProvider dateTimeProvider)
    {
        _localStorage = localStorage;
        _http = http;
        _dateTimeProvider = dateTimeProvider;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = (await _localStorage.GetItemAsStringAsync(LocalStorageKeys.Token))?.Replace("\"", "");
        var refreshToken = (await _localStorage.GetItemAsStringAsync(LocalStorageKeys.RefreshToken))?.Replace("\"", "");

        var identity = new ClaimsIdentity();
        _http.DefaultRequestHeaders.Authorization = null;

        if (!string.IsNullOrEmpty(token) && token != "null")
        {
            var claims = ParseClaimsFromJwt(token).ToList();
            var expiration = claims.Where(c => c.Type == "exp").FirstOrDefault();

            if (expiration != null)
            {
                var expirationDate = DateTimeOffset.FromUnixTimeSeconds(long.Parse(expiration.Value));
                var now = _dateTimeProvider.UtcNow;

                if (expirationDate < now)
                {
                    var authResult = await RefreshTokenAsync(token, refreshToken);
                    if (authResult is null)
                    {
                        return CreateAuthenticationState(identity);
                    }
                    claims = ParseClaimsFromJwt(token).ToList();
                }
            }

            identity = new ClaimsIdentity(claims, "jwt", "name", null);

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return CreateAuthenticationState(identity);
    }

    private AuthenticationState CreateAuthenticationState(ClaimsIdentity identity)
    {
        var user = new ClaimsPrincipal(identity);
        var state = new AuthenticationState(user);

        NotifyAuthenticationStateChanged(Task.FromResult(state));
        return state;
    }

    private async Task<AuthenticationResponse?> RefreshTokenAsync(string token, string refreshToken)
    {
        var refreshRequest = new TokenRefreshRequest(token, refreshToken);
        using var response = await _http.PostAsJsonAsync("api/auth/refresh", refreshRequest);

        if(!response.IsSuccessStatusCode)
        {
            await _localStorage.RemoveItemsAsync(new List<string> { LocalStorageKeys.Token, LocalStorageKeys.RefreshToken });
            return null;
        }

        var authResult = await response.Content.ReadFromJsonAsync<AuthenticationResponse>();

        await _localStorage.SetItemAsync(LocalStorageKeys.Token, authResult.Token);
        await _localStorage.SetItemAsync(LocalStorageKeys.RefreshToken, authResult.RefreshToken);

        return authResult;
    }

    public static IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var jsonBytes = ParseBase64WithoutPadding(payload);
        var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes) ??
            throw new InvalidOperationException("Invalid JWT payload");

        return keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value?.ToString() ?? ""));
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