using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RandomSteamGameBlazor.Server.Authentication.Common;
using RandomSteamGameBlazor.Server.Common.Exceptions;
using RandomSteamGameBlazor.Server.Common.Services;

namespace RandomSteamGameBlazor.Server.Authentication.Commands;

public class TokenRefreshHandler : IRequestHandler<TokenRefreshCommand, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly UserManager<RandomSteamUser> _userManager;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtSettings _jwtSettings;

    public TokenRefreshHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        UserManager<RandomSteamUser> userManager,
        IDateTimeProvider dateTimeProvider,
        IOptions<JwtSettings> jwtSettings)
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _userManager = userManager;
        _dateTimeProvider = dateTimeProvider;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthenticationResult> Handle(TokenRefreshCommand request, CancellationToken cancellationToken)
    {
        var principalResult = _jwtTokenGenerator.GetPrincipalFromExpiredToke(request.token);


        var username = principalResult.Identity.Name;
        var user = await _userManager.FindByNameAsync(username);

        if (user is null ||
            user.RefreshToken != request.refreshToken ||
            user.RefreshTokenExpiryTime <= _dateTimeProvider.UtcNow)
        {
            throw new AuthenticationException("Invalid Token");
        }

        var newToken = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays);

        await _userManager.UpdateAsync(user);

        return new AuthenticationResult(user, newToken, newRefreshToken);
    }
}
