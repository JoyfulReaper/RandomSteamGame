using ErrorOr;
using MediatR;
using SteamApiClient.HttpClients;

namespace RandomSteamGameBlazor.Server.Features.Steam.Queries.ResolveVantiy;

public class ResolveVanityQueryHandler : IRequestHandler<ResolveVanityQuery, ErrorOr<long>>
{
    private readonly ISteamClient _steamClient;
    private readonly ILogger<ResolveVanityQueryHandler> _logger;

    public ResolveVanityQueryHandler(
        ISteamClient steamClient,
        ILogger<ResolveVanityQueryHandler> logger)
    {
        _steamClient = steamClient;
        _logger = logger;
    }

    public async Task<ErrorOr<long>> Handle(ResolveVanityQuery request, CancellationToken cancellationToken)
    {
        try
        {
            return await _steamClient.GetSteamIdFromVanityUrl(request.vantiyUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve vanity URL: {vanityUrl}", request.vantiyUrl);
            return Errors.Steam.VanityResolutonFailed;
        }
    }
}
