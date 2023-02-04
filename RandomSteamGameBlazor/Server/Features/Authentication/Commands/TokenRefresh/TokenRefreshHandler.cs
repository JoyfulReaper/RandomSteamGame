using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RandomSteamGameBlazor.Server.Common.Services;
using RandomSteamGameBlazor.Server.Common.Errors;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;
using System.Security.Authentication;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands.TokenRefresh;

public class TokenRefreshHandler : IRequestHandler<TokenRefreshCommand, ErrorOr<AuthenticationResult>>
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

    public async Task<ErrorOr<AuthenticationResult>> Handle(TokenRefreshCommand request, CancellationToken cancellationToken)
    {
        var principalResult = _jwtTokenGenerator.GetPrincipalFromExpiredToken(request.token);
        if (principalResult.IsError)
        {
            return principalResult.Errors;
        }

        if (principalResult.Value.Identity is null)
        {
            return Errors.Authentication.InvalidCredentials;
        }

        var username = principalResult.Value.Identity.Name;
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