using Blazored.LocalStorage;
using MediatR;
using RandomSteamGameBlazor.Client.Common;

namespace RandomSteamGameBlazor.Client.Features.Authentication.Queries;

public class LogOutQueryHandler : IRequestHandler<LogOutQuery, Unit>
{
    private readonly ILocalStorageService _localStorageService;
    private readonly AuthenticationStateProvider _authenticationStateProvider;

    public LogOutQueryHandler(
        ILocalStorageService localStorageService,
        AuthenticationStateProvider authenticationStateProvider)
    {
        _localStorageService = localStorageService;
        _authenticationStateProvider = authenticationStateProvider;
    }

    public async Task<Unit> Handle(LogOutQuery request, CancellationToken cancellationToken)
    {
        await _localStorageService.RemoveItemsAsync(new List<string> {
            LocalStorageKeys.Token,
            LocalStorageKeys.RefreshToken
        }, cancellationToken);

        await _authenticationStateProvider.GetAuthenticationStateAsync();
        
        return Unit.Value;
    }
}