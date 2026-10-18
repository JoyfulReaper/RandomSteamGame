using RandomSteamGame.MatrixBot.Matrix;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace RandomSteamGame.MatrixBot.MatrixClient;

public class MatrixClient
{
    private readonly HttpClient _httpClient;
    private readonly string _password;
    private readonly string _userName;

    public MatrixClient(HttpClient httpClient, string userName, string password)
    {
        _httpClient = httpClient;
        _password = password;
        _userName = userName;
    }

    public async Task LoginAsync()
    {
        var loginRequest = new LoginRequest("m.login.password", new Identifer("m.id.user", _userName), _password);
        using var response = await _httpClient.PostAsJsonAsync("https://matrix.kgivler.com/_matrix/client/v3/login", loginRequest);

        if (response.IsSuccessStatusCode)
        {
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>()
                ?? throw new Exception("Failed to login");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);
        }
        else
        {
            throw new Exception("Failed to login");
        }
    }

    public async Task<WhoAmIResponse?> WhoAmIAsync()
    {
        var whoAmiResponse = await _httpClient.GetFromJsonAsync<WhoAmIResponse>("https://matrix.kgivler.com/_matrix/client/v3/account/whoami");
        return whoAmiResponse;
    }

    public async Task<SyncResponse?> SyncAsync(string? since, CancellationToken ct = default)
    {
        var syncResponse = await _httpClient.GetFromJsonAsync<SyncResponse>("https://matrix.kgivler.com/_matrix/client/v3/sync?timeout=30000");
        return syncResponse;
    }
}

public record LoginRequest(string Type, Identifer Identifer, string Password);
public record Identifer(string Type, string User);
