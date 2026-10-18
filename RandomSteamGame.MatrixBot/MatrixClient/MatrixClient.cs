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

    public async Task LoginAsync(CancellationToken ct = default)
    {
        var loginRequest = new LoginRequest("m.login.password", new Identifier("m.id.user", _userName), _password);
        using var response = await _httpClient.PostAsJsonAsync("_matrix/client/v3/login", loginRequest, ct);

        if (response.IsSuccessStatusCode)
        {
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(ct)
                ?? throw new Exception("Failed to login");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);
        }
        else
        {
            throw new Exception("Failed to login");
        }
    }

    public async Task<WhoAmIResponse?> WhoAmIAsync(CancellationToken ct = default)
    {
        var whoAmiResponse = await _httpClient.GetFromJsonAsync<WhoAmIResponse>("_matrix/client/v3/account/whoami", ct);
        return whoAmiResponse;
    }

    public async Task<SyncResponse?> SyncAsync(string? since, CancellationToken ct = default)
    {
        var url = since is null
            ? $"_matrix/client/v3/sync?timeout=30000"
            : $"_matrix/client/v3/sync?since={Uri.EscapeDataString(since)}&timeout=30000";
        var syncResponse = await _httpClient.GetFromJsonAsync<SyncResponse>(url, ct);
        return syncResponse;
    }

    public async Task JoinRoomAsync(string roomIdOrAlias, CancellationToken ct = default)
    {
        using var response = await _httpClient.PostAsJsonAsync($"/_matrix/client/v3/join/{Uri.EscapeDataString(roomIdOrAlias)}", new { }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"Join failed: HTTP {(int)response.StatusCode}");
            Console.WriteLine(error);
        }


        response.EnsureSuccessStatusCode();

        //var joinResponse = await response.Content.ReadFromJsonAsync<JoinResponse>(ct);
        //if (joinResponse is null)
        //{
        //    throw new Exception("Failed to join room");
        //}
    }
}

public record LoginRequest(string Type, Identifier Identifier, string Password);
public record Identifier(string Type, string User);
