using MediatR;
using RandomSteamGameBlazor.Server.Features.Authentication.Common;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Queries;


public record LoginQuery(
    string Email,
    string Password) : IRequest<AuthenticationResult>;