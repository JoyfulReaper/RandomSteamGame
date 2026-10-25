using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RandomSteamGame.Shared.Contracts;

public sealed record FeatureProposalResponse(
    long Id, string Slug, string Title, string Description, string Status,
    long? GitHubIssueNumber, long VoteCount, bool Supported);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class FeatureVoteRequest
{
    [Required]
    public bool? Supported { get; init; }
}
