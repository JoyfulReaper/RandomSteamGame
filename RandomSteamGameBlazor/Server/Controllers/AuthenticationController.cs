/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Services;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthenticationController : ApiController
{
    private readonly IAuthService _authService;
    private readonly IMapper _mapper;

    public AuthenticationController(
        IAuthService authService,
        IMapper mapper)
    {
        _authService = authService;
        _mapper = mapper;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        return result.Match(
            res => Ok(_mapper.Map<AuthenticationResponse>(res)),
            Problem);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        return result.Match(
            res => Ok(_mapper.Map<AuthenticationResponse>(res)),
            Problem);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(TokenRefreshRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);

        return result.Match(
            res => Ok(_mapper.Map<AuthenticationResponse>(res)),
            Problem);
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke()
    {
        var result = await _authService.RevokeTokenAsync(User.Identity?.Name);

        return result.Match(
            res => NoContent(),
            Problem);
    }
}