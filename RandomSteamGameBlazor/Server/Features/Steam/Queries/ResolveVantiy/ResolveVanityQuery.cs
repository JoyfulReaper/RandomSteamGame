using ErrorOr;
using MediatR;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.ResolveVantiy;

public record ResolveVanityQuery(string vantiyUrl) : IRequest<ErrorOr<long>>;