/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using ErrorOr;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Services;

public interface IAuthService
{
    Task<ErrorOr<AuthenticationResult>> RegisterAsync(RegisterRequest request);
    Task<ErrorOr<AuthenticationResult>> LoginAsync(LoginRequest request);
    Task<ErrorOr<AuthenticationResult>> RefreshTokenAsync(TokenRefreshRequest request);
    Task<ErrorOr<Success>> RevokeTokenAsync(string? email);
}