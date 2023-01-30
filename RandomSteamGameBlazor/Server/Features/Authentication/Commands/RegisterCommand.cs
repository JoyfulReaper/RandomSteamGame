using MediatR;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<AuthenticationResult>;
