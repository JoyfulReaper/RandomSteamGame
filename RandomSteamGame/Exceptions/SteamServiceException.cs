namespace RandomSteamGame.Exceptions;

public class SteamServiceException : Exception
{
    public SteamServiceException()
    {
    }

    public SteamServiceException(string message)
        : base(message)
    {
    }

    public SteamServiceException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
