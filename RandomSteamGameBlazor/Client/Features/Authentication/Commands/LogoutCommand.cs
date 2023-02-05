using MediatR;

namespace RandomSteamGameBlazor.Client.Features.Authentication.Queries;

public record LogoutCommand() : IRequest<Unit>;