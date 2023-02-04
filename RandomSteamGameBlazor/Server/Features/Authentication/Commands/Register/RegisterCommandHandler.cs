using ErrorOr;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RandomSteamGameBlazor.Server.Common.Services;
using RandomSteamGameBlazor.Server.Features.Authentication.Commands.Register;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands;

public class RegisterCommandHandler :
    IRequestHandler<RegisterCommand, ErrorOr<AuthenticationResult>>
{
    private readonly UserManager<RandomSteamUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtSettings _jwtSettings;

    public RegisterCommandHandler(UserManager<RandomSteamUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<ErrorOr<AuthenticationResult>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = new RandomSteamUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors =
            result.Errors.Select(e => Error.Validation(e.Code, e.Description))
            .ToList();

            return errors;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);

        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays);
        await _userManager.UpdateAsync(user);

        return new AuthenticationResult(user, token, refreshToken);
    }
}
