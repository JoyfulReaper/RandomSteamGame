using RandomSteamGame.Shared.Contracts;

namespace RandomSteamGame.Tests;

public sealed class FeatureProposalSeedTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Script => File.ReadAllText(RepositoryFile("docs/sql/initial-feature-proposals.sql"));

    [Fact]
    public async Task ManualScriptAddsExactlyTheTenOpenProposalsWithoutInventedIssues()
    {
        using var db = new VotingTestDatabase();
        db.Execute(Script);
        var features = await db.Service.GetAsync(new string('a', 64), Ct);
        Assert.Equal(new[] { "permanent-blocked-games", "favorites", "personal-picker-history", "game-queue", "saved-game-lists",
            "shareable-game-lists", "advanced-picker-filters", "extended-csv-export", "manually-added-games", "other-game-services" }, features.Select(p => p.Slug));
        Assert.All(features, p => { Assert.Equal("open", p.Status); Assert.Null(p.GitHubIssueNumber); Assert.Equal(0, p.VoteCount); });
        Assert.Contains("global Recent Picks", features.Single(p => p.Slug == "personal-picker-history").Description);
        Assert.Contains("current basic export already", features.Single(p => p.Slug == "extended-csv-export").Description);
        Assert.Contains("existing unplayed-only", features.Single(p => p.Slug == "advanced-picker-filters").Description);
        Assert.Contains("substantial work", features.Single(p => p.Slug == "other-game-services").Description);
        Assert.Contains("legally and practically", features.Single(p => p.Slug == "other-game-services").Description);
        Assert.Contains("before paying", features.Single(p => p.Slug == "other-game-services").Description);
    }

    [Fact]
    public async Task RepeatedManualScriptPreservesCuratedFieldsOtherProposalsAndVotes()
    {
        using var db = new VotingTestDatabase();
        db.Execute(Script);
        var browser = new string('b', 64);
        var feature = (await db.Service.GetAsync(browser, Ct)).Single(p => p.Slug == "favorites");
        await db.Service.SetSupportedAsync(feature.Id, browser, true, Ct);
        db.Execute("UPDATE FeatureProposals SET Status='planned', Title='Curated title', Description='Curated description', GitHubIssueNumber=123 WHERE Slug='favorites';");
        db.AddProposal(1000);
        var before = await db.Service.GetAsync(browser, Ct);
        var timestamp = db.Scalar("SELECT CreatedAtUtc FROM FeatureVotes;");
        db.Execute(Script);
        db.Execute(Script);
        Assert.Equal(before, await db.Service.GetAsync(browser, Ct));
        Assert.Equal(timestamp, db.Scalar("SELECT CreatedAtUtc FROM FeatureVotes;"));
        Assert.Equal(1L, db.Scalar("SELECT COUNT(*) FROM FeatureVotes;"));
    }

    internal static string RepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RandomSteamGame.slnx"))) directory = directory.Parent;
        return Path.Combine((directory ?? throw new DirectoryNotFoundException("Repository root not found.")).FullName, relativePath);
    }
}
