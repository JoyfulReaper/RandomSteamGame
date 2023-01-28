using MediatR;

namespace RandomSteamGameBlazor.Server.Steam.Queries.ResolveVantiy;

public record ResolveVanityQuery(string vantiyUrl) : IRequest<long>;