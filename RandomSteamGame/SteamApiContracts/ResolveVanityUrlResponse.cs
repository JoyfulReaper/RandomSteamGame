namespace RandomSteamGame.SteamApiContracts;

public class ResolveVanityUrlResponse
{
    public Response Response { get; set; } = default!;
}

public class Response
{
    public string? SteamId { get; set; }
    public int Success { get; set; }
}
