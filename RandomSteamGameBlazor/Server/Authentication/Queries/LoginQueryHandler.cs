using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RandomSteamGameBlazor.Server.Authentication.Common;
using RandomSteamGameBlazor.Server.Common.Exceptions;
using RandomSteamGameBlazor.Server.Common.Services;

namespace RandomSteamGameBlazor.Server.Authentication.Queries;

public class LoginQueryHandler : IRequestHandler<LoginQuery, AuthenticationResult>
{
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtSettings _jwtSettings;
    private readonly UserManager<RandomSteamUser> _userManager;
    private readonly SignInManager<RandomSteamUser> _signInManager;

    public LoginQueryHandler(
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IOptions<JwtSettings> jwtSettings,
        UserManager<RandomSteamUser> userManager,
        SignInManager<RandomSteamUser> signInManager)
        
    {
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _jwtSettings = jwtSettings.Value;
        _userManager = userManager;
        _signInManager = signInManager;
    }
    
    public async Task<AuthenticationResult> Handle(LoginQuery request, CancellationToken cancellationToken)
    {
        RandomSteamUser? user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new AuthenticationException("Invalid Credentials");
        }

        SignInResult result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
        if (!result.Succeeded)
        {
            throw new AuthenticationException("Invalid Credentials");
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays);
        await _userManager.UpdateAsync(user);

        return new AuthenticationResult(user, token, refreshToken);
    }
}
