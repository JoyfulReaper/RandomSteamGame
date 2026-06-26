using RandomSteamGame.Client.Services.Interfaces;

namespace RandomSteamGame.Client.Services;

public class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}