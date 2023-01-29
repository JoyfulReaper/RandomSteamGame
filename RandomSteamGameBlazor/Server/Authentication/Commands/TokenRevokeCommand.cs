using MediatR;

namespace RandomSteamGameBlazor.Server.Authentication.Commands;

public record TokenRevokeCommand(string email) : IRequest<Unit>;
