-- Run manually against the initialized application Data/kgivler_com.db.
-- This script is never run by application startup. It creates no tables.
-- Existing slugs, descriptions, statuses, issue associations, IDs, and votes are preserved.
BEGIN IMMEDIATE;

INSERT INTO FeatureProposals (Slug, Title, Description, Status, GitHubIssueNumber)
VALUES
    ('permanent-blocked-games', 'Permanent blocked games',
     'Keep blocked games across browsers and devices instead of only in browser cookies. Requires Steam sign-in support.', 'open', NULL),
    ('favorites', 'Favorites',
     'Save games you want to come back to, linked to your future account. Requires Steam sign-in support.', 'open', NULL),
    ('personal-picker-history', 'Personal picker history',
     'Revisit your own past picks and optionally avoid recent repeats. This would be personal history, separate from the global Recent Picks display, and requires Steam sign-in support.', 'open', NULL),
    ('game-queue', 'Game queue',
     'Build an ordered queue of games to play next, alongside the random picker. Saving it across devices requires Steam sign-in support.', 'open', NULL),
    ('saved-game-lists', 'Saved game lists',
     'Organize games into lists such as short games, co-op, or play later, then pick from a list. Account-linked lists require Steam sign-in support.', 'open', NULL),
    ('shareable-game-lists', 'Shareable game lists',
     'Share selected saved lists through a link so other people can browse them. Creating account-linked lists requires Steam sign-in support.', 'open', NULL),
    ('advanced-picker-filters', 'Advanced picker filters',
     'Extend the existing unplayed-only and blocked-game filters with options such as playtime ranges, genres, or Steam Deck compatibility, where reliable data is available.', 'open', NULL),
    ('extended-csv-export', 'Extended CSV export',
     'Add optional richer metadata to the existing CSV export, such as genres, developers, or pricing where available. The current basic export already includes playtime and Steam Deck compatibility.', 'open', NULL),
    ('manually-added-games', 'Manually added games',
     'Add your own game entries alongside Steam games for picking and lists. Saving those entries to an account requires Steam sign-in support.', 'open', NULL),
    ('other-game-services', 'Other game services',
     'Explore other libraries using the provider-oriented architecture where practical. This could be substantial work even with that abstraction: feasibility depends on a service offering an API that can legally and practically be used. Interested sponsors should contact Kyle about the specific service before paying.', 'open', NULL)
ON CONFLICT(Slug) DO NOTHING;

COMMIT;
