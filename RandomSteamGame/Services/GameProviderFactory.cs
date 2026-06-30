using System.Diagnostics.CodeAnalysis;
using RandomSteamGame.Services.Interfaces;

namespace RandomSteamGame.Services;

public sealed class GameProviderFactory
{
    private readonly IReadOnlyDictionary<string, IGameProvider> _providers;

    public GameProviderFactory(IEnumerable<IGameProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.ProviderKey,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetProvider(
        string providerKey,
        [NotNullWhen(true)] out IGameProvider? provider)
    {
        return _providers.TryGetValue(providerKey, out provider);
    }
}
