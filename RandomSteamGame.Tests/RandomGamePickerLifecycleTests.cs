using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using RandomSteamGame.Client.Components;
using System.Collections;
using System.Reflection;

namespace RandomSteamGame.Tests;

public sealed class RandomGamePickerLifecycleTests
{
    [Fact]
    public async Task DisposeAsyncUnsubscribesAndAwaitsModuleDisposalExactlyOnce()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new FakeModule { OnDispose = () => new ValueTask(completion.Task) };
        var runtime = new FakeRuntime(() => Task.FromResult<IJSObjectReference>(module));
        var (component, callbacks) = CreateComponent(runtime);
        component.InitializeAgain();
        Assert.Single(callbacks.Cast<object>());
        await component.RenderAfterAsync();
        Assert.Equal(1, runtime.Imports);
        Assert.Equal("./Components/RandomGamePicker.razor.js", runtime.ImportPath);
        Assert.Equal(1, module.ThemeUpdates);

        var disposal = component.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        Assert.Empty(callbacks.Cast<object>());
        Assert.Null(GetModule(component));
        Assert.Equal(1, module.Disposals);
        completion.SetResult();
        await disposal.WaitAsync(TestContext.Current.CancellationToken);
        await component.DisposeAsync();
        await component.RenderAfterAsync();
        Assert.Equal(1, module.Disposals);
        Assert.Equal(1, runtime.Imports);
    }

    [Fact]
    public async Task DisposeAsyncBeforeImportMakesNoInteropCalls()
    {
        var module = new FakeModule();
        var runtime = new FakeRuntime(() => Task.FromResult<IJSObjectReference>(module));
        var (component, callbacks) = CreateComponent(runtime);
        await component.DisposeAsync();
        await component.RenderAfterAsync();
        Assert.Empty(callbacks.Cast<object>());
        Assert.Equal(0, runtime.Imports);
        Assert.Equal(0, module.Disposals);
    }

    [Fact]
    public async Task DisposeAsyncAfterFailedImportMakesNoAdditionalInteropCalls()
    {
        var runtime = new FakeRuntime(() => Task.FromException<IJSObjectReference>(new JSException("Import failed.")));
        var (component, callbacks) = CreateComponent(runtime);
        await component.RenderAfterAsync();
        Assert.Null(GetModule(component));
        await component.DisposeAsync();
        Assert.Empty(callbacks.Cast<object>());
        Assert.Equal(1, runtime.Imports);
    }

    [Fact]
    public async Task DisposeAsyncToleratesDisconnectedCircuit()
    {
        var module = new FakeModule { OnDispose = () => ValueTask.FromException(new JSDisconnectedException("Circuit closed.")) };
        var (component, callbacks) = CreateComponent(new FakeRuntime(() => Task.FromResult<IJSObjectReference>(module)));
        await component.RenderAfterAsync();
        await component.DisposeAsync();
        await component.DisposeAsync();
        Assert.Empty(callbacks.Cast<object>());
        Assert.Null(GetModule(component));
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task DisposeAsyncDoesNotSwallowUnrelatedExceptions()
    {
        var failure = new InvalidOperationException("Unexpected disposal failure.");
        var module = new FakeModule { OnDispose = () => ValueTask.FromException(failure) };
        var (component, callbacks) = CreateComponent(new FakeRuntime(() => Task.FromResult<IJSObjectReference>(module)));
        await component.RenderAfterAsync();
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => component.DisposeAsync().AsTask()));
        Assert.Empty(callbacks.Cast<object>());
        Assert.Null(GetModule(component));
        await component.DisposeAsync();
        Assert.Equal(1, module.Disposals);
    }

    [Fact]
    public async Task ImportCompletingAfterDisposalIsReleasedWithoutUpdatingTheme()
    {
        var import = new TaskCompletionSource<IJSObjectReference>(TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new FakeModule();
        var runtime = new FakeRuntime(() => import.Task);
        var (component, callbacks) = CreateComponent(runtime);
        var render = component.RenderAfterAsync();
        Assert.Equal(1, runtime.Imports);
        await component.DisposeAsync();
        Assert.Empty(callbacks.Cast<object>());
        import.SetResult(module);
        await render.WaitAsync(TestContext.Current.CancellationToken);
        await component.DisposeAsync();
        Assert.Equal(1, module.Disposals);
        Assert.Equal(0, module.ThemeUpdates);
        Assert.Null(GetModule(component));
    }

    private static (TestPicker Component, ICollection Callbacks) CreateComponent(IJSRuntime runtime)
    {
        var manager = new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance);
        var component = new TestPicker();
        // Exercise the real lifecycle methods without adding a UI framework. Reflection supplies
        // Razor's private injected properties and observes Blazor's subscription collection.
        SetInjection(component, "ApplicationState", manager.State);
        SetInjection(component, "JSRuntime", runtime);
        SetInjection(component, "Logger", NullLogger<RandomGamePicker>.Instance);
        component.InitializeAgain();
        var callbacks = Assert.IsAssignableFrom<ICollection>(typeof(PersistentComponentState)
            .GetField("_registeredCallbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager.State));
        Assert.Single(callbacks.Cast<object>());
        return (component, callbacks);
    }

    private static void SetInjection(TestPicker component, string property, object value) =>
        typeof(RandomGamePicker).GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(component, value);

    private static object? GetModule(TestPicker component) => typeof(RandomGamePicker)
        .GetField("_themeModule", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component);

    private sealed class TestPicker : RandomGamePicker
    {
        public void InitializeAgain() => OnInitialized();
        public Task RenderAfterAsync() => OnAfterRenderAsync(true);
    }

    private sealed class FakeRuntime(Func<Task<IJSObjectReference>> import) : IJSRuntime
    {
        public int Imports { get; private set; }
        public string? ImportPath { get; private set; }
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier);
            ImportPath = Assert.IsType<string>(Assert.Single(args!));
            Imports++;
            return (TValue)(object)await import();
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class FakeModule : IJSObjectReference
    {
        public Func<ValueTask> OnDispose { get; init; } = () => ValueTask.CompletedTask;
        public int Disposals { get; private set; }
        public int ThemeUpdates { get; private set; }
        public ValueTask DisposeAsync()
        {
            Disposals++;
            return OnDispose();
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("updateTheme", identifier);
            ThemeUpdates++;
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
