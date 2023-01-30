global using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using RandomSteamGameBlazor.Client;
using RandomSteamGameBlazor.Client.Features.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
{
    builder.RootComponents.Add<App>("#app");
    builder.RootComponents.Add<HeadOutlet>("head::after");
    
    builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
    builder.Services.AddHttpClient();

    builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddAuthorizationCore();

    builder.Services.AddBlazoredLocalStorage();

    await builder.Build().RunAsync();
}