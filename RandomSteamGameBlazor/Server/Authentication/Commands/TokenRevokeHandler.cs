using MediatR;
using Microsoft.AspNetCore.Identity;
using RandomSteamGameBlazor.Server.Common.Exceptions;

namespace RandomSteamGameBlazor.Server.Authentication.Commands;

public class TokenRevokeHandler : IRequestHandler<TokenRevokeCommand, Unit>
{
    private readonly UserManager<RandomSteamUser> _userManager;

    public TokenRevokeHandler(UserManager<RandomSteamUser> userManager)
    {
        _userManager = userManager;
    }
    
    public async Task<Unit> Handle(TokenRevokeCommand request, CancellationToken cancellationToken)
    {
        if (request.email is null)
        {
            throw new AuthenticationException("Invalid Credentials");
        }

        var user = await _userManager.FindByEmailAsync(request.email);

        if (user is null)
        {
            throw new AuthenticationException("Invalid Credentials");
        }

        user.RefreshToken = null;
        await _userManager.UpdateAsync(user);

        return new Unit();
    }
}
