namespace RandomSteamGame.Events;

public sealed record LibraryExportCompletedEvent(
    string? VisitorId,
    string Provider,
    string IngressNetwork,
    int GameCount,
    long DurationMilliseconds,
    int VerifiedCount,
    int PlayableCount,
    int UnsupportedCount,
    int UnknownCount,
    string? CommitSha);