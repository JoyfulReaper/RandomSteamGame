using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamApiClient.Contracts.SteamApi;

public class ResolveVanityUrlResponse
{
    public ResolvedVanityUrl response { get; set; } = default!;
}

public class ResolvedVanityUrl
{
    public string? SteamId { get; set; }
    public int Success { get; set; }
}

