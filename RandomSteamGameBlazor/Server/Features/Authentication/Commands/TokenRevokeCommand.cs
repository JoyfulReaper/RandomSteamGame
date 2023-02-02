using ErrorOr;
using MediatR;

namespace RandomSteamGameBlazor.Server.Features.Authentication.Commands;

public record TokenRevokeCommand(string? email) : IRequest<ErrorOr<Success>>;
