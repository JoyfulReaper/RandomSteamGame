namespace RandomSteamGameBlazor.Server.Common.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}