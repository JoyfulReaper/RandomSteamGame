using MediatR;
using SteamApiClient.HttpClients;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.ResolveVantiy;

public class ResolveVanityHandler : IRequestHandler<ResolveVanityQuery, long>
{
    private readonly SteamClient _steamClient;

    public ResolveVanityHandler(SteamClient steamClient)
    {
        _steamClient = steamClient;
    }

    public async Task<long> Handle(ResolveVanityQuery request, CancellationToken cancellationToken)
    {
        return await _steamClient.GetSteamIdFromVanityUrl(request.vantiyUrl);
    }
}
