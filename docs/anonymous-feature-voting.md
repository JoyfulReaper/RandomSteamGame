# Anonymous feature voting: persistence and API

This is the backend foundation only. The support page is unchanged, no voting UI
exists yet, and no proposals are seeded. Suggestions remain GitHub issues; only
developer-curated proposals will eventually appear on the board. No accounts,
Steam identity associations, sponsorship terms, or payment processing are involved.

## Schema and persistence

Startup adds two tables to the existing `Data/kgivler_com.db`, beneath
`AppContext.BaseDirectory`, using idempotent `CREATE TABLE IF NOT EXISTS` statements.
It does not update or delete existing rows, seed proposals, or change journal mode.

- `FeatureProposals`: integer `Id` primary key; unique `Slug` (1–80 lowercase ASCII
  letters/digits/hyphens, no leading/trailing hyphen); `Title` (1–160 characters);
  `Description` (1–4000 characters); `Status` (`open` or `closed`); nullable positive
  `GitHubIssueNumber`. Titles/descriptions must contain non-space content and should
  be treated as plain text by future UI. IDs and slugs are stable curation identifiers.
- `FeatureVotes`: `FeatureProposalId` foreign key; lowercase hexadecimal SHA-256
  `BrowserIdentityHash` (64 characters); round-trip UTC `CreatedAtUtc` text. The
  composite primary key `(FeatureProposalId, BrowserIdentityHash)` prevents duplicate
  support. There are no IP, IP-hash, Steam-ID, user-agent, or contact fields.

Each operation opens its own connection with foreign keys enabled, default cache
mode, and a one-second busy timeout. Voting has its own process-wide asynchronous
write gate with a one-second admission timeout, independent of recent-pick history.
An immediate transaction checks proposal status and changes a vote atomically;
SQLite remains the cross-connection/process writer authority. Duplicate support
does not change the original creation timestamp. Withdrawal deletes only that
browser's row. Totals are calculated from stored votes.

There is no public curation API. Proposal insertion and lifecycle tooling are
deliberately deferred; do not delete/recreate a database to populate the board.

## Browser cookie

`GET /api/features` establishes `.RandomSteamGame.FeatureVoter.v1` when necessary.
The cookie contains a Data Protection protected, time-limited, cryptographically
random 256-bit identifier. Only SHA-256 of the underlying random bytes is persisted
in SQLite. Neither the identifier nor its hash appears in API responses.

The cookie is HttpOnly, SameSite=Lax, host-only, scoped to `/api/features`, and
expires after 365 days without sliding renewal. Public deployments use Secure
cookies, including development, matching server-written Steam identity cookies.
AltNet derives Secure from its configured canonical origin: HTTP permits a
non-Secure cookie; HTTPS requires Secure even when the backend receives HTTP.
Use HTTPS for Public local API testing.

Missing, malformed, tampered, or expired cookies are replaced by GET. POST never
silently creates a replacement identity: it returns 400 and requires a fresh GET.
Clients must wait for that GET and retain cookies before sending concurrent votes.
Deleting cookies, using private browsing, switching browsers/hosts, or losing the
Data Protection keys creates a new identity. Existing vote rows remain. This is
anonymous browser-based support, **not verified one-person-one-vote**.

## API contract

All feature API responses, including validation/security failures, carry
`Cache-Control: private, no-store` and `CDN-Cache-Control: no-store`. Feature endpoints
disable CORS; browser clients should use same-origin requests with cookies.

`GET /api/features` returns 200 with a JSON array, ordered by stable numeric ID:

```json
[
  {
    "id": 1,
    "slug": "example-proposal",
    "title": "Example proposal",
    "description": "Illustrative contract only; this proposal is not seeded.",
    "status": "open",
    "gitHubIssueNumber": null,
    "voteCount": 3,
    "supported": true
  }
]
```

Both open and closed proposals are returned, including retained totals/selections.
An empty installation returns `[]`.

`POST /api/features/{id}/vote` requires a positive Int64 proposal ID, the established
voter cookie, JSON content type, and the existing antiforgery cookie/request-token
pair. Obtain tokens through `GET /api/antiforgery/token`, retain its cookie, and send
the returned `requestToken` in the returned `headerName` (currently
`RequestVerificationToken`). Do not hardcode the header name in future clients.

```json
{ "supported": true }
```

Use `false` to withdraw. The boolean is required; null, wrong types, malformed JSON,
and unknown properties are rejected. The request body limit is 1024 bytes. A browser
may support multiple proposals. Repeated identical mutations return the same
successful outcome without creating additional votes.

- 204: support/withdrawal saved, including an already-satisfied request.
- 400: invalid input, invalid antiforgery pair, or absent/invalid voter cookie.
- 404: proposal does not exist.
- 409: proposal is closed; both additions and withdrawals are rejected.
- 413/415: oversized body/unsupported content type.
- 429: rate/capacity limit, with `Retry-After` seconds.
- 503: SQLite unavailable/contended or local write admission timed out, with
  `Retry-After: 1`. Retry the same desired state after waiting.
- 500: unexpected failure, with a generic ProblemDetails response.

GET never changes votes. Errors do not expose internal exception text. Future
InteractiveAuto components should make these calls through browser fetch, as the
existing library export does, so cookies, antiforgery, effective ingress IP, and
HTTP rate limiting apply equally to Server and WebAssembly execution. Do not call
the voting store directly from interactive components.

## Abuse resistance and ingress

The existing trusted-proxy middleware runs before voting admission. Voting uses
only its resulting `RemoteIpAddress`; it never independently reads forwarding
headers. Preserve the ingress configuration and require trusted proxies to
overwrite visitor-supplied forwarding headers.

IP partitions are HMAC-SHA256 with an ephemeral random server key and a
voting-specific domain prefix. IPv4-mapped addresses normalize to IPv4; IPv6
interfaces share a /64 partition, matching existing export normalization. Null
addresses share an unknown partition. The hashes exist only in transient limiter
memory, are not logged/persisted by voting, and cannot correlate with telemetry
visitor hashes. Idle partitions are reclaimed by the framework. No additional
HMAC deployment secret is needed.

The voting-specific limits are independent of the Steam/export policies:

| Setting | Default | Scope per `WindowSeconds` |
| --- | --- | --- |
| `FeatureVoting:BrowserPermitLimit` | 20 | POST requests per protected browser identity |
| `FeatureVoting:IpPermitLimit` | 120 | GET + POST per effective IP signal |
| `FeatureVoting:GlobalPermitLimit` | 600 | GET + POST across this process |
| `FeatureVoting:GlobalConcurrency` | 8 | Simultaneous GET + POST, no queue |
| `FeatureVoting:WindowSeconds` | 60 | Fixed-window duration |

All settings validate at startup. IP limits are request-rate ceilings, not unique
voter restrictions: different browsers behind one address can each vote. Invalid
antiforgery requests are rejected before voting admission and application writes.
The GET that issues an identity is also subject to IP/global admission. A client
may rotate cookies/IPs, so global admission remains necessary. Fixed-window
boundaries permit bursts; these controls are modest abuse resistance, not a bot
identity service or protection against volumetric denial of service.

## Deployment and verification

Persist the existing application `Data/` volume and Data Protection key ring, and
keep the Data Protection application name stable. Existing keys must remain
readable after deployments; losing keys prevents browsers from managing their
previous votes. There is no schema seed, new database, external service, or journal
configuration requirement. Take the usual database backup before releasing any
schema addition. Existing readiness checks verify the shared database and key
directory; they do not independently inspect the voting tables.

Configure limits with `FeatureVoting__*` environment variables when needed.
Shared-address AltNet/tunnel deployments may need a higher IP request allowance;
do not enable forwarding unless that ingress supplies trustworthy client addresses.
Public and AltNet hosts have separate host-only voter cookies. If their backing
database is shared, one browser can consequently appear as multiple voters across
hosts. Decide a cross-host policy before exposing the board on multiple origins.

Admission and IP keys reset on restart and are process-local. Database uniqueness
survives restart. Multiple replicas have independent request limits; this slice
does not introduce distributed limiting or change the existing SQLite deployment
model. Existing homepage hit statistics can separately retain IP addresses; voting
does not join or expand that data.

Focused coverage is in `FeatureVotingServiceTests` and `FeatureVotingHttpTests`:
new/existing schema initialization, uniqueness, multiple proposals/browsers,
withdrawal, concurrent requests, closed/missing proposals, antiforgery, cookies,
trusted ingress, all admission layers, contention, and controlled failures.

```powershell
dotnet test RandomSteamGame.Tests/RandomSteamGame.Tests.csproj --filter FullyQualifiedName~FeatureVoting
dotnet build RandomSteamGame.slnx --no-restore
dotnet test RandomSteamGame.slnx --no-restore
```
