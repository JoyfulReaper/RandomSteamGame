using ErrorOr;
using MapsterMapper;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RandomSteamGameBlazor.Server.Features.Authentication.Commands.Register;
using RandomSteamGameBlazor.Server.Features.Authentication.Commands.TokenRefresh;
using RandomSteamGameBlazor.Server.Features.Authentication.Commands.TokenRevoke;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;
using RandomSteamGameBlazor.Server.Features.Authentication.Queries.Login;
using RandomSteamGameBlazor.Shared.Contracts.Authentication;

namespace RandomSteamGameBlazor.Server.Controllers;

[Route("api/auth")]
[ApiController]
[AllowAnonymous]
public class AuthenticationController : ApiController
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

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var command = _mapper.Map<RegisterCommand>(request);
        ErrorOr<AuthenticationResult> result = await _mediator.Send(command);

        return result.Match(
            result => Ok(_mapper.Map<AuthenticationResponse>(result)),
            errors => Problem(errors));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var query = _mapper.Map<LoginQuery>(request);
        ErrorOr<AuthenticationResult> result = await _mediator.Send(query);

        return result.Match(
            result => Ok(_mapper.Map<AuthenticationResponse>(result)),
            errors => Problem(errors));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(TokenRefreshRequest request)
    {
        var query = _mapper.Map<TokenRefreshCommand>(request);
        ErrorOr<AuthenticationResult> result = await _mediator.Send(query);

        return result.Match(
            result => Ok(_mapper.Map<AuthenticationResponse>(result)),
            errors => Problem(errors));
    }

    [Authorize]
    [HttpPost("revoke")]
    public async Task<IActionResult> Revoke()
    {
        var query = new TokenRevokeCommand(User.Identity?.Name);
        var result = await _mediator.Send(query);

        return result.Match(
            result => NoContent(),
            errors => Problem(errors));
    }
}