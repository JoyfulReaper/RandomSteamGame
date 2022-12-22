namespace RandomSteamGame.Exceptions;

public class VanityResolutionException : Exception
{
    public VanityResolutionException()
    {
    }

    public VanityResolutionException(string message)
        : base(message)
    {
    }

    public VanityResolutionException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
