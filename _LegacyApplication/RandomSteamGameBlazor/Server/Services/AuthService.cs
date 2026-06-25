/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RandomSteamGameBlazor.Server.Common.Errors;
using RandomSteamGameBlazor.Server.Common.Services;
using RandomSteamGameBlazor.Server.Features.Authentication;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<RandomSteamUser> _userManager;
    private readonly SignInManager<RandomSteamUser> _signInManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<RandomSteamUser> userManager,
        SignInManager<RandomSteamUser> signInManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IDateTimeProvider dateTimeProvider,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _dateTimeProvider = dateTimeProvider;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<ErrorOr<AuthenticationResult>> RegisterAsync(RegisterRequest request)
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
            return result.Errors.Select(e => Error.Validation(e.Code, e.Description)).ToList();
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays);
        await _userManager.UpdateAsync(user);

        return new AuthenticationResult(user, token, refreshToken);
    }

    public async Task<ErrorOr<AuthenticationResult>> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Errors.Authentication.InvalidCredentials;
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, true);
        if (!result.Succeeded)
        {
            return Errors.Authentication.InvalidCredentials;
        }

        var token = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays);
        await _userManager.UpdateAsync(user);

        return new AuthenticationResult(user, token, refreshToken);
    }

    public async Task<ErrorOr<AuthenticationResult>> RefreshTokenAsync(TokenRefreshRequest request)
    {
        var principalResult = _jwtTokenGenerator.GetPrincipalFromExpiredToken(request.Token);
        if (principalResult.IsError || principalResult.Value.Identity is null)
        {
            return Errors.Authentication.InvalidCredentials;
        }

        var username = principalResult.Value.Identity.Name;
        var user = await _userManager.FindByNameAsync(username!);

        if (user is null || user.RefreshToken != request.RefreshToken || user.RefreshTokenExpiryTime <= _dateTimeProvider.UtcNow)
        {
            return Errors.Authentication.InvalidToken;
        }

        var newToken = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = _dateTimeProvider.UtcNow.AddDays(_jwtSettings.RefreshExpirationDays);
        await _userManager.UpdateAsync(user);

        return new AuthenticationResult(user, newToken, newRefreshToken);
    }

    public async Task<ErrorOr<Success>> RevokeTokenAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Errors.Authentication.InvalidCredentials;

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return Errors.Authentication.InvalidCredentials;

        user.RefreshToken = null;
        await _userManager.UpdateAsync(user);

        return new Success();
    }
}