using Microsoft.AspNetCore.Components;

namespace RandomSteamGame.Shared.Services
{
    public class BackendApiClient
    {
        public HttpClient Client { get; }

        public BackendApiClient(HttpClient client, NavigationManager navManager)
        {
            client.BaseAddress = new Uri(navManager.BaseUri);
            Client = client;
        }
    }
}