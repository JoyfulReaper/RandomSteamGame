using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Identity;
using RandomSteamGameBlazor.Server.Common.Errors;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands.TokenRevoke;

public class TokenRevokeHandler : IRequestHandler<TokenRevokeCommand, ErrorOr<Success>>
{
    private readonly UserManager<RandomSteamUser> _userManager;

    public TokenRevokeHandler(UserManager<RandomSteamUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<ErrorOr<Success>> Handle(TokenRevokeCommand request, CancellationToken cancellationToken)
    {
        if (request.email is null)
        {
            return Errors.Authentication.InvalidCredentials;
        }

        var user = await _userManager.FindByEmailAsync(request.email);

        if (user is null)
        {
            return Errors.Authentication.InvalidCredentials;
        }

        user.RefreshToken = null;
        await _userManager.UpdateAsync(user);

        return new Success();
    }
}
