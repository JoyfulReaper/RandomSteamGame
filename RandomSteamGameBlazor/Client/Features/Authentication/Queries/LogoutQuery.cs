using MediatR;

namespace RandomSteamGameBlazor.Client.Features.Authentication.Queries;

public record LogOutQuery() : IRequest<Unit>;