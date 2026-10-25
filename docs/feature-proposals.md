# Support page and curated proposals

`/support` provides optional Ko-fi/PayPal donations, feature voting, GitHub
suggestions, private sponsorship inquiries, and Kyle's portfolio. The existing
PayPal destination is preserved, including its suggested amount. Donations do not
purchase features; sponsorship terms must be agreed with Kyle before payment.

## Browser voting

The shell and links render on the server. On Public deployments,
`FeatureVotingBoard` uses InteractiveAuto and imports `js/featureVoting.js` only
after interactive rendering. Even during Interactive Server execution, JavaScript
fetch sends requests from the browser. The component makes no direct store calls
or server loopback requests.

`GET /api/features` establishes the existing voter cookie and returns selections.
Each mutation fetches `/api/antiforgery/token`, uses its returned header name/token,
and posts only `{ "supported": true }` or `false`. All requests use same-origin
mode, same-origin credentials, and no-store caching. The backend continues to own
identity, antiforgery, uniqueness, and rate/capacity limits.

Open proposals appear first, ordered by votes descending and numeric ID ascending.
Other statuses follow in ID order without voting controls. Labels are Open for
votes, Planned, Available, and Not planned. Neither votes nor planned status imply
a delivery commitment. All cards link to Kyle's contact form; visitors should name
the feature when asking about sponsorship.

The board handles loading, empty lists, pending operations, and safe errors.
Successful writes refresh totals/selections. If that refresh fails, the confirmed
selection remains visible with a warning that totals may be stale. Lost mutation
responses are uncertain: reload before voting again. There are no automatic write
retries. Identity/proposal conflicts refresh the list without retrying the write.
429/503 responses show Retry-After seconds when available. Voting controls are
disabled while an operation is pending. Counts refresh on load, manual reload, and
mutations; there is no background polling.

Native buttons, pressed states, descriptive labels, live messages, visible focus,
and wrapping mobile layouts support keyboard and small-screen use. Cookies remain
browser/host-specific; this does not verify unique people or synchronize devices.
AltNet renders a quiet availability message and keeps all other support links,
without instantiating the board. Public clients also handle API 404 quietly.
This does not replace the server gate. See [the backend contract](anonymous-feature-voting.md)
for the process-wide NetworkMode boundary, persistence, and deployment limits.

## Manually populate the board

Startup creates the schema but never inserts proposals. The ten initial proposals
are in [initial-feature-proposals.sql](sql/initial-feature-proposals.sql). All start
open with stable slugs and NULL issue numbers. Steam sign-in is a prerequisite for
account-linked ideas, not a separate proposal. History, filters, and CSV descriptions
distinguish current features from extensions; other-service support depends on a
legally/practically usable API and invites discussion of the service before payment.

Run the app once to initialize a fresh database. Before a manual write, identify
the real application database and take the normal backup. Its path is
`Data/kgivler_com.db` beneath the binary directory (normally
`/app/Data/kgivler_com.db` in Docker), **not** `steam_cache.db`.

With an existing SQLite CLI on the machine/container owning that database:

```sh
sqlite3 -bail "/absolute/path/Data/kgivler_com.db" ".read /absolute/path/initial-feature-proposals.sql"
sqlite3 "/absolute/path/Data/kgivler_com.db" "SELECT Id, Slug, Title, Status FROM FeatureProposals ORDER BY Id;"
```

Substitute actual absolute paths. The short transaction uses
`ON CONFLICT(Slug) DO NOTHING`: reruns add missing slugs and preserve existing titles,
descriptions, statuses, issue associations, IDs, and votes. It creates no schema,
never reopens an existing proposal, and does not delete/reset a database. This PR
does not run the script against production. Running it later is an operator action.

## Maintain proposals manually

These SQLite examples add no administration API/dashboard. Keep published slugs
stable and escape apostrophes in SQL text as `''`.

```sql
INSERT INTO FeatureProposals (Slug, Title, Description, Status, GitHubIssueNumber)
VALUES ('your-feature-slug', 'Your feature', 'A concise description.', 'open', NULL)
ON CONFLICT(Slug) DO NOTHING;

-- Allowed statuses: open, planned, completed, closed. Only open accepts mutations.
UPDATE FeatureProposals SET Status = 'planned' WHERE Slug = 'your-feature-slug';

UPDATE FeatureProposals
SET Description = 'An updated description.' WHERE Slug = 'your-feature-slug';

-- Bind $verified_issue_number in your SQLite tool only after verifying an actual
-- issue in JoyfulReaper/RandomSteamGame. Leave NULL when no issue exists.
UPDATE FeatureProposals SET GitHubIssueNumber = $verified_issue_number
WHERE Slug = 'your-feature-slug';
```

Use a transaction for related edits, verify affected rows, and close proposals
instead of deleting/recreating them. Every status retains totals/selections;
non-open proposals reject both vote additions and removals.

## Verification

Component tests use the framework renderer for real Razor output/events in Server
and WebAssembly modes. Node tests check browser fetch/antiforgery sequencing and
safe HTTP/network failures. HTTP tests cover Public/AltNet shells and existing
links. Seed tests execute the real script against temporary databases, including
reruns after curation and votes. No new test framework/dependency is introduced.

```sh
dotnet test RandomSteamGame.Tests/RandomSteamGame.Tests.csproj --filter "FullyQualifiedName~FeatureVoting|FullyQualifiedName~FeatureProposalSeed|FullyQualifiedName~SupportPage"
node --test RandomSteamGame.Tests/Browser/featureVoting.test.mjs
dotnet test RandomSteamGame.slnx --no-restore
```

Before release, check fresh/returning browsers, actual HTTPS cookies, keyboard
focus, mobile widths, and an AltNet deployment. Renderer tests simulate execution
modes; they do not replace full browser WebAssembly boot or CSS layout checks.

The local preview was checked with empty and seeded boards, voting, withdrawal,
selection persistence after reload, and keyboard focus after sorting. Desktop
(1280 px) and mobile (390 px) checks found no horizontal overflow. Screenshots:
[desktop](images/support-desktop.jpg) and [mobile](images/support-mobile.jpg).
Only the isolated temporary preview database was manually populated; production
was not accessed. Production HTTPS/cookie behavior and an actual AltNet ingress
remain release checks.
