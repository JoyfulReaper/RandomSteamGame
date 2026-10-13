namespace RandomSteamGame.Services;

// Capture request data without retaining an HttpContext throughout a Blazor circuit.
public sealed record GameRequestContext(string? VisitorIpAddress, string? Host, IReadOnlyCollection<int> ExcludedGameIds)
{
    public static GameRequestContext From(HttpContext? context) => new(
        context?.Connection.RemoteIpAddress?.ToString(),
        context?.Request.Host.Host,
        GameSelectionHelper.ParseExcludedGameIds(context?.Request.Cookies["ExcludedGameIds"]).ToArray());
}
