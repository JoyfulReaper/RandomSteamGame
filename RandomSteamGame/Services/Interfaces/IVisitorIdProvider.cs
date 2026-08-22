namespace RandomSteamGame.Services.Interfaces;

public interface IVisitorIdProvider
{
    string? GetVisitorId(string ipAddress);
}
