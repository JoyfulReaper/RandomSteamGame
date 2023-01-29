using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Authentication.Commands;
using RandomSteamGameBlazor.Server.Authentication.Common;
using RandomSteamGameBlazor.Server.Authentication.Queries;
using RandomSteamGameBlazor.Server.Common.Exceptions;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Controllers;

[Route("api/auth")]
[ApiController]
public class AuthenticationController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IMapper _mapper;
    private readonly ILogger _logger;

    public AuthenticationController(
        ISender mediator,
        IMapper mapper,
        ILogger<AuthenticationController> logger)
    {
        _mediator = mediator;
        _mapper = mapper;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        try
        {
            var command = _mapper.Map<RegisterCommand>(request);
            AuthenticationResult result = await _mediator.Send(command);

            return Ok(_mapper.Map<AuthenticationResponse>(result));
        }
        catch (AuthenticationException ex)
        {
            return Problem(ex.Message);
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        try
        {
            var query = _mapper.Map<LoginQuery>(request);
            AuthenticationResult result = await _mediator.Send(query);

            return Ok(_mapper.Map<AuthenticationResponse>(result));
        }
        catch (AuthenticationException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(TokenRefreshRequest request)
    {
        try
        {
            var query = _mapper.Map<TokenRefreshCommand>(request);
            AuthenticationResult result = await _mediator.Send(query);

            return Ok(_mapper.Map<AuthenticationResponse>(result));
        }
        catch (AuthenticationException)
        {
            return Unauthorized();
        }
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke()
    {
        try
        {
            var query = new TokenRevokeCommand(User.Identity.Name);
            var result = await _mediator.Send(query);

            return NoContent();
        }
        catch (AuthenticationException)
        {
            return Unauthorized();
        }
    }
}
