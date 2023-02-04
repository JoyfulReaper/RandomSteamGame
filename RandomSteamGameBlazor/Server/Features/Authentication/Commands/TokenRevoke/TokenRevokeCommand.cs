using ErrorOr;
using MediatR;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands.TokenRevoke;

public record TokenRevokeCommand(string? email) : IRequest<ErrorOr<Success>>;
