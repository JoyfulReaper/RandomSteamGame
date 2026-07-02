using Microsoft.JSInterop;
using RandomSteamGame.Client.Services;

namespace RandomSteamGame.Tests;

public class BrowserSteamIdentityStoreTests
{
    [Fact]
    public async Task SetExcludedGameIdsAsync_KeepsNewestOneHundredUniqueIds()
    {
        var module = new RecordingCookieModule();
        var store = new BrowserSteamIdentityStore(new FakeJSRuntime(module));
        var appIds = Enumerable.Range(1, 102)
            .Concat([50, 0, -1, 103]);

        await store.SetExcludedGameIdsAsync(appIds);

        var savedIds = Assert.IsType<string>(module.SetCookieArgs[1])
            .Split(',')
            .Select(int.Parse)
            .ToArray();

        Assert.Equal(100, savedIds.Length);
        Assert.Equal(100, savedIds.Distinct().Count());
        Assert.DoesNotContain(1, savedIds);
        Assert.DoesNotContain(2, savedIds);
        Assert.DoesNotContain(0, savedIds);
        Assert.DoesNotContain(-1, savedIds);
        Assert.Equal(50, savedIds[^2]);
        Assert.Equal(103, savedIds[^1]);
    }

    private sealed class FakeJSRuntime : IJSRuntime
    {
        private readonly IJSObjectReference _module;

        public FakeJSRuntime(IJSObjectReference module)
        {
            _module = module;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            return new ValueTask<TValue>((TValue)_module);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }
    }

    private sealed class RecordingCookieModule : IJSObjectReference
    {
        public object?[] SetCookieArgs { get; private set; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "setCookie")
            {
                SetCookieArgs = args ?? [];
            }

            return new ValueTask<TValue>(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            return InvokeAsync<TValue>(identifier, args);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
