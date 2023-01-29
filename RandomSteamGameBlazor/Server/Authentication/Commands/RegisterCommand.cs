using MediatR;
using RandomSteamGameBlazor.Server.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Authentication.Commands;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<AuthenticationResult>;
