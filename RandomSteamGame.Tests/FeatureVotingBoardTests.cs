using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using RandomSteamGame.Client.Components;
using RandomSteamGame.Client.Models;
using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Tests;

// The framework renderer exercises the real Razor output/events without adding a UI test dependency.
#pragma warning disable BL0006
public sealed class FeatureVotingBoardTests
{
    private static FeatureProposalResponse Proposal(long id = 1, string status = "open", long votes = 0, bool supported = false) =>
        new(id, $"feature-{id}", $"Feature {id}", "A useful feature.", status, null, votes, supported);
    private static FeatureVotingResult Board(params FeatureProposalResponse[] proposals) => new() { Status = 200, Proposals = proposals };

    [Fact]
    public async Task PrerenderShowsLoadingWithoutBrowserInterop()
    {
        await using var harness = new BoardHarness(interactive: false);
        await harness.StartAsync();
        Assert.Contains("Loading features", (await harness.DocumentAsync()).Body!.TextContent);
        Assert.Equal(0, harness.Runtime.Imports);
    }

    [Theory]
    [InlineData("Server")]
    [InlineData("WebAssembly")]
    public async Task BothAutoExecutionModesUseTheSameBrowserModuleAndHandleEmptyProposals(string rendererName)
    {
        await using var harness = new BoardHarness(rendererName: rendererName);
        await harness.StartAsync();
        Assert.Equal("./js/featureVoting.js", harness.Runtime.ImportPath);
        Assert.Equal(1, harness.Runtime.Imports);
        Assert.Equal(1, harness.Runtime.Module.Loads);
        var document = await harness.DocumentAsync();
        Assert.Contains("No proposals are listed yet", document.Body!.TextContent);
        Assert.Empty(document.QuerySelectorAll(".vote-button"));
    }

    [Fact]
    public async Task ProposalsAreSortedWithStableTiesAndNonOpenStatusesHaveNoVotingControls()
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = Board(Proposal(4, "completed", 100), Proposal(3, votes: 2),
            Proposal(2, votes: 5), Proposal(1, votes: 5), Proposal(5, "planned"), Proposal(6, "closed"));
        await harness.StartAsync();
        var document = await harness.DocumentAsync();
        var cards = document.QuerySelectorAll("article");
        Assert.Equal(new[] { "Feature 1", "Feature 2", "Feature 3", "Feature 4", "Feature 5", "Feature 6" }, cards.Select(c => c.QuerySelector("h3")!.TextContent));
        Assert.Equal(3, document.QuerySelectorAll(".vote-button").Length);
        Assert.Contains("Available", cards[3].TextContent);
        Assert.Contains("Planned", cards[4].TextContent);
        Assert.Contains("Not planned", cards[5].TextContent);
        Assert.All(cards, c => Assert.Equal("https://dev.kgivler.com/#contact", c.QuerySelector("a.feature-sponsor")!.GetAttribute("href")));
    }

    [Fact]
    public async Task VoteAndWithdrawalRefreshCountsAndSelections()
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = Board(Proposal());
        await harness.StartAsync();
        harness.Runtime.Module.NextBoard = Board(Proposal(votes: 7, supported: true));
        await harness.ClickAsync("Vote for Feature 1");
        var document = await harness.DocumentAsync();
        Assert.Contains("7 votes", document.Body!.TextContent);
        Assert.Contains("Vote added.", document.Body.TextContent);
        Assert.Equal("true", document.QuerySelector(".vote-button")!.GetAttribute("aria-pressed"));
        harness.Runtime.Module.NextBoard = Board(Proposal(votes: 6));
        await harness.ClickAsync("Remove vote for Feature 1");
        document = await harness.DocumentAsync();
        Assert.Contains("6 votes", document.Body!.TextContent);
        Assert.Contains("Vote removed.", document.Body.TextContent);
        Assert.Equal(new[] { true, false }, harness.Runtime.Module.Supports);
        Assert.Equal(3, harness.Runtime.Module.Loads);
    }

    [Fact]
    public async Task PendingVoteDisablesControlsAndKeepsTheCurrentCards()
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = Board(Proposal());
        await harness.StartAsync();
        var pending = new TaskCompletionSource<FeatureVotingResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Runtime.Module.Mutation = () => pending.Task;
        var click = harness.ClickAsync("Vote for Feature 1");
        var document = await harness.DocumentAsync();
        Assert.True(document.QuerySelector(".vote-button")!.HasAttribute("disabled"));
        Assert.Contains("Saving your vote", document.Body!.TextContent);
        harness.Runtime.Module.NextBoard = Board(Proposal(votes: 1, supported: true));
        pending.SetResult(new() { Status = 204 });
        await click;
        Assert.False((await harness.DocumentAsync()).QuerySelector(".vote-button")!.HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(400, "session was refreshed")]
    [InlineData(409, "no longer open")]
    [InlineData(404, "no longer available")]
    [InlineData(429, "17 seconds")]
    [InlineData(503, "temporarily unavailable")]
    [InlineData(0, "Check your connection")]
    [InlineData(500, "couldn't confirm")]
    public async Task MutationFailuresHaveSafeUsefulMessages(int status, string message)
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = Board(Proposal());
        await harness.StartAsync();
        harness.Runtime.Module.Mutation = () => Task.FromResult(new FeatureVotingResult { Status = status, RetryAfterSeconds = 17 });
        if (status == 409) harness.Runtime.Module.NextBoard = Board(Proposal(status: "planned"));
        await harness.ClickAsync("Vote for Feature 1");
        var document = await harness.DocumentAsync();
        Assert.Contains(message, document.Body!.TextContent);
        Assert.NotNull(document.QuerySelector("[role=alert]"));
        if (status == 409) Assert.Empty(document.QuerySelectorAll(".vote-button"));
        if (status == 0) Assert.True(document.QuerySelector(".vote-button")!.HasAttribute("disabled"));
    }

    [Fact]
    public async Task DisabledVotingIsQuietAndOffersReload()
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = new() { Status = 404 };
        await harness.StartAsync();
        var document = await harness.DocumentAsync();
        Assert.Contains("isn't available", document.Body!.TextContent);
        Assert.Null(document.QuerySelector("[role=alert]"));
        Assert.NotNull(document.QuerySelector(".reload-button"));
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(500)]
    [InlineData(0)]
    public async Task InitialHttpFailuresAllowReloadWithoutVotingControls(int status)
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = new() { Status = status };
        await harness.StartAsync();
        var document = await harness.DocumentAsync();
        Assert.NotNull(document.QuerySelector("[role=alert]"));
        Assert.Empty(document.QuerySelectorAll(".vote-button"));
        harness.Runtime.Module.NextBoard = Board(Proposal());
        await harness.ClickAsync("Reload features");
        Assert.NotNull((await harness.DocumentAsync()).QuerySelector(".vote-button"));
    }

    [Fact]
    public async Task ConfirmedVoteSurvivesFailedRefreshWithoutPretendingCountsAreCurrent()
    {
        await using var harness = new BoardHarness();
        harness.Runtime.Module.NextBoard = Board(Proposal());
        await harness.StartAsync();
        harness.Runtime.Module.NextBoard = new() { Status = 503 };
        await harness.ClickAsync("Vote for Feature 1");
        var document = await harness.DocumentAsync();
        Assert.Contains("Your vote was saved", document.Body!.TextContent);
        Assert.Equal("true", document.QuerySelector(".vote-button")!.GetAttribute("aria-pressed"));
        Assert.True(document.QuerySelector(".vote-button")!.HasAttribute("disabled"));
    }

    [Fact]
    public async Task ImportFailuresAndUntrustedProposalTextDoNotLeakOrRenderHtml()
    {
        await using var harness = new BoardHarness();
        harness.Runtime.ImportFailure = true;
        await harness.StartAsync();
        Assert.DoesNotContain("internal-secret", (await harness.DocumentAsync()).Body!.TextContent);
        harness.Runtime.ImportFailure = false;
        harness.Runtime.Module.NextBoard = Board(Proposal() with { Title = "<script>alert('title')</script>", Description = "<img src=x onerror=alert(1)>" });
        await harness.ClickAsync("Reload features");
        var document = await harness.DocumentAsync();
        Assert.Empty(document.QuerySelectorAll("script,img"));
        Assert.Contains("<img", document.Body!.TextContent);
    }

    private sealed class BoardHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private readonly InteractiveHtmlRenderer _renderer;
        private HtmlRootComponent _root;
        public BrowserRuntime Runtime { get; } = new();

        public BoardHarness(bool interactive = true, string rendererName = "Server")
        {
            _services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime>(Runtime).BuildServiceProvider();
            _renderer = new(_services, interactive, rendererName);
        }
        public Task StartAsync() => _renderer.Dispatcher.InvokeAsync(() =>
        {
            _root = _renderer.Start();
            return _root.QuiescenceTask;
        });
        public Task<IDocument> DocumentAsync() => _renderer.Dispatcher.InvokeAsync(() =>
            Task.FromResult<IDocument>(new HtmlParser().ParseDocument(_root.ToHtmlString())));
        public Task ClickAsync(string label) => _renderer.Dispatcher.InvokeAsync(() => _renderer.ClickAsync(label));
        public async ValueTask DisposeAsync() { await _renderer.DisposeAsync(); await _services.DisposeAsync(); }
    }

    private sealed class InteractiveHtmlRenderer(IServiceProvider services, bool interactive, string name)
        : StaticHtmlRenderer(services, NullLoggerFactory.Instance)
    {
        private IComponent _component = null!;
        protected override RendererInfo RendererInfo => new(name, interactive);
        protected override Task UpdateDisplayAsync(in RenderBatch batch) => interactive ? Task.CompletedTask : Task.FromCanceled(new CancellationToken(true));
        public HtmlRootComponent Start()
        {
            _component = InstantiateComponent(typeof(FeatureVotingBoard));
            return BeginRenderingComponent(_component, ParameterView.Empty);
        }
        public Task ClickAsync(string label)
        {
            var frames = GetCurrentRenderTreeFrames(GetComponentState(_component).ComponentId);
            for (var i = 0; i < frames.Count; i++)
            {
                if (frames.Array[i].FrameType != RenderTreeFrameType.Element || frames.Array[i].ElementName != "button") continue;
                var end = i + frames.Array[i].ElementSubtreeLength;
                ulong handler = 0;
                var matches = false;
                for (var j = i + 1; j < end; j++)
                {
                    var frame = frames.Array[j];
                    if (frame.FrameType == RenderTreeFrameType.Attribute)
                    {
                        if (frame.AttributeName == "onclick") handler = frame.AttributeEventHandlerId;
                        if (frame.AttributeName == "aria-label" && Equals(frame.AttributeValue, label)) matches = true;
                    }
                    else if (frame.FrameType == RenderTreeFrameType.Text && frame.TextContent == label) matches = true;
                }
                if (matches && handler != 0) return DispatchEventAsync(handler, null, new MouseEventArgs());
            }
            throw new InvalidOperationException($"Button not found: {label}");
        }
    }

    private sealed class BrowserRuntime : IJSRuntime
    {
        public BrowserModule Module { get; } = new();
        public int Imports { get; private set; }
        public string? ImportPath { get; private set; }
        public bool ImportFailure { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier);
            Imports++;
            ImportPath = (string)args![0]!;
            if (ImportFailure) throw new JSException("internal-secret");
            return ValueTask.FromResult((TValue)(object)Module);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
    private sealed class BrowserModule : IJSObjectReference
    {
        public FeatureVotingResult NextBoard { get; set; } = Board();
        public Func<Task<FeatureVotingResult>> Mutation { get; set; } = () => Task.FromResult(new FeatureVotingResult { Status = 204 });
        public List<bool> Supports { get; } = [];
        public int Loads { get; private set; }
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "focusVote") return default!;
            if (identifier == "load") { Loads++; return (TValue)(object)NextBoard; }
            Assert.Equal("setSupported", identifier);
            Supports.Add((bool)args![1]!);
            return (TValue)(object)await Mutation();
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken ct, object?[]? args) => InvokeAsync<TValue>(identifier, args);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
