namespace RandomSteamGameBlazor.Client.Common.Services;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}