using MediatR;
using RandomSteamGameBlazor.Server.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Authentication.Queries;


public record LoginQuery(
    string Email,
    string Password) : IRequest<AuthenticationResult>;