using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Services;

public class GameProviderFactory
{
    private readonly IEnumerable<IGameProvider> _providers;

    public GameProviderFactory(IEnumerable<IGameProvider> providers)
    {
        _providers = providers;
    }

    public IGameProvider GetProvider(string providerKey)
    {
        return _providers.FirstOrDefault(p => p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase))
               ?? throw new NotSupportedException($"Provider {providerKey} not supported.");
    }
}