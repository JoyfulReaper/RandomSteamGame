# Technical privacy and data-processing inventory

This intentionally public document is an engineering reference, **not the public
Privacy Policy or legal advice**. The [Privacy Policy](../RandomSteamGame/Components/Pages/Privacy.razor)
is available on the site at `/privacy`.
It records current behavior; it does not authorize changes to collection, retention,
production configuration, or databases.

## Scope and evidence

Verified against `dev` at `2b70fdd33dfae129d2d22f3d5b851baa4795a20b` on 2026-10-25.
The earlier audit covered `1266bcd` plus PR #48 at `a426673`. PR #48 is now merged
as `8b99597`; the subsequent About page/navigation change adds ordinary links and
static content, without changing the audited persistence or voting code.

- **Verified** means behavior found in application source or the pinned dependency
  implementations linked below. It does not prove what production is running.
- **Configuration-dependent** means tracked defaults or explicit configuration
  determine behavior; deployed values are unknown unless operator-confirmed below.
- **Operational unknown** means source cannot establish the answer. Resolve the
  questions below before making public promises about retention or deletion.

Use [developer handoff](developer-handoff.md) for architecture,
[voting backend](anonymous-feature-voting.md) for the API/security contract, and
[proposal maintenance](feature-proposals.md) for curation. This document covers
their data-processing implications rather than repeating implementation instructions.

### Operator-confirmed production behavior

Kyle confirmed the following production behavior on 2026-10-25. These are operator
statements, not conclusions established by the tracked defaults or a live inspection:

- The Public deployment sends telemetry to Mission Control.
- The Mission Control archive currently retains events indefinitely.
- The I2P deployment disables Mission Control telemetry.
- I2P hides visitors' public IP addresses from the application, but local proxy
  addresses and server logs may still exist. This does not mean the deployment has
  no address records or diagnostics.

The source-verified behavior below explains the available mechanisms. In particular,
`NetworkMode=AltNet` alone does not disable telemetry; the I2P telemetry setting is a
confirmed deployment choice, not a guarantee for every AltNet instance. Reconfirm
these statements if production configuration or archive handling changes. Other
operational details remain listed in [the open questions](#operational-questions).

## Data collected and why

The site has no Steam authentication or user account model. An entered public Steam
identifier is not proof that the visitor owns that account. Public availability,
hashing, or lack of a real name does not make information non-identifying.

| Information | Collection trigger and purpose | Where it goes / association |
| --- | --- | --- |
| Effective client IP, hit count, last-seen time | Homepage rendering records visits and estimates unique visitors | Persisted in `Visitors`, independently of telemetry settings; see retention below |
| Steam ID / normalized vanity name | Picker, library lookup, export, or vanity resolution; locate a public library | Identity cookies, identifier-bearing URLs, backend Steam requests and cache keys; may appear in diagnostics |
| Public library game IDs/names, icon references, game count, total/recent/platform playtime, last-played timestamps | Read Steam library information for selection and CSV export | Cached under Steam ID; selected game information reaches the browser |
| Unplayed-only preference, excluded game IDs, refresh timestamp | Save selections, block games, or refresh library | Browser cookies; matching first-party requests transmit cookies to the server; exclusions are used server-side |
| Random browser voter identifier and SHA-256 representation | Public voting-board initialization and mutations; remember selections and prevent duplicate browser/proposal votes | Protected identifier in cookie; hash shared across that browser's vote rows, without a Steam association |
| User agent, optional IP-derived telemetry ID, ingress label, operation metrics | Homepage/picker/export telemetry, if enabled | Mission Control; no visitor cookie is needed for this telemetry ID |
| Request URLs, exceptions, selected game IDs/names and operational context | Navigation, API calls and diagnostics | Browser history/console, application logs, and potentially infrastructure logs |
| Curated proposal text and public game metadata | Operator curation and Steam metadata lookup | Public board/cache; not a visitor profile, but free text can contain names or other identifying content |

Sources: [homepage collection](../RandomSteamGame/Components/Pages/Home.razor#L393-L407),
[statistics processing](../RandomSteamGame/Services/AppStatsService.cs#L42-L78),
[library fields](../SteamApiClient/Contracts/SteamApi/OwnedGameResponse.cs#L12-L30),
[Steam requests/cache keys](../SteamApiClient/HttpClients/SteamClient.cs),
[browser identity store](../RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs),
[voting identity](../RandomSteamGame/Services/FeatureVotingIdentity.cs), and
[proposal/vote schema](../RandomSteamGame/Services/SqliteFeatureVotingService.cs#L11-L28).

## Cookies and browser storage

Currently created cookies are host-only (no `Domain`) and `SameSite=Lax`.
JavaScript-written cookies use `Secure` when the browser's protocol is HTTPS.
Server-written identity cookies use `Secure` in Public, and follow the configured
canonical origin's scheme in AltNet. Antiforgery follows that deployment policy,
except Public development uses the effective request scheme. Voting cookies are
Secure in Public, including development; voting is unavailable in AltNet.

| Exact cookie name | Value, creator/reader and trigger | Lifetime, path, HttpOnly | Deletion and role |
| --- | --- | --- | --- |
| `SteamId` | Plain numeric identifier saved by the browser store on picker/export submission; browser/server identity readers reuse it. A server writer implements the same identity convention | 365 days from writing; `/`; no | Expiry/browser clearing or identity-clear methods. Remembered input, not an authentication session; no visible forget-identity control found |
| `UnplayedOnly` | Boolean preference written with identity; browser/server readers | 365 days from writing; `/`; no | Overwrite, expiry, browser clearing or identity-clear methods; optional preference |
| `VanityUrl` | Legacy fallback read by identity readers; current writers delete it when saving numeric identity | No current creation; original legacy expiry/attributes not established | Explicit deletion on numeric identity save/clear, or browser clearing |
| `ExcludedGameIds` | Comma-separated positive game IDs, at most 100, saved when blocking games; browser and server selection code read them | 365 days from writing; `/`; no | Reset blocked games, writing an empty list, expiry or browser clearing; optional filtering |
| `OwnedGamesCacheResetAt` | UTC refresh timestamp saved after successful refresh; read during picker hydration | 2 days; `/`; no | Removed when UI detects the 12-hour cooldown has passed, or expiry/browser clearing; refresh UX state |
| `.RandomSteamGame.FeatureVoter.v1` | Data Protection protected random 256-bit identifier. GET `/api/features` creates/replaces missing, malformed or expired identity; POST only reads it | 365 days with protected-token expiry, Expires and Max-Age; **no sliding renewal**; `/api/features`; **yes** | Expiry/browser clearing; no identity-delete endpoint. Necessary for optional browser voting, not for using the picker. Cookie deletion does **not** delete votes |
| `.RandomSteamGame.Antiforgery.v2` | Protected security token; framework form rendering or `/api/antiforgery/token` issues it, mutations validate the cookie/request-token pair | Session cookie, no configured persistent expiry; `/`; **yes** | Browser clearing/session handling; session restore may preserve it. Security support for forms/export/voting, not an account or analytics identifier |

Cookie evidence: [names](../RandomSteamGame.Shared/Contracts/SteamIdentityCookies.cs),
[browser values/lifetimes/deletion](../RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs#L21-L153),
[JS attributes](../RandomSteamGame.Client/wwwroot/js/cookieHelper.js#L18-L31),
[server writer](../RandomSteamGame/Services/ServerSteamIdentityWriter.cs#L25-L72),
[deployment policy](../RandomSteamGame/Services/DeploymentCookiePolicy.cs),
[voter protection/expiry](../RandomSteamGame/Services/FeatureVotingIdentity.cs#L8-L48),
[antiforgery configuration](../RandomSteamGame/Extensions/ServiceExtensions.cs#L113-L121),
[token endpoint](../RandomSteamGame/Controllers/AntiforgeryController.cs), and
[framework cookie defaults](https://github.com/dotnet/aspnetcore/blob/v10.0.13/src/Antiforgery/src/AntiforgeryOptions.cs).

**Automatic creation matters:** prerendered `EditForm` handling can issue antiforgery
before form submission. On Public `/support`, the board automatically performs GET
after becoming interactive, even without a vote or any proposals. Prerender itself
does not create voter identity. Mutations fetch the antiforgery request token through
browser JavaScript, including during Interactive Server execution. See
[forms](../RandomSteamGame.Client/Components/SteamPickerForm.razor#L37),
[board lifecycle](../RandomSteamGame.Client/Components/FeatureVotingBoard.razor#L75-L117),
[browser fetch](../RandomSteamGame.Client/wwwroot/js/featureVoting.js), and
[framework form handling](https://github.com/dotnet/aspnetcore/blob/v10.0.13/src/Components/Web/src/Forms/EditForm.cs#L140-L170).

These observed purposes are functional/security-related. Neither that description
nor a framework `IsEssential` flag establishes a legal exemption from consent requirements.

| Other browser state | Data, lifetime and deletion |
| --- | --- |
| Blazor localStorage | Framework key `blazor-resource-hash:${mainAssemblyName}` holds a resource-version hash used by InteractiveAuto, not a visitor identifier. No TTL there; overwritten by later resource versions or removed by clearing site storage. [Framework source](https://github.com/dotnet/aspnetcore/blob/v10.0.13/src/Components/Web.JS/src/Services/WebRootComponentManager.ts#L520-L547) |
| Prerender/component state | `steamPickerFormState` carries identity/preferences; `random-game-details:{Provider}:{SteamId}:{UnplayedOnly}` carries result/error state across rendering. Page/component state is not a persistent localStorage account database. [Form state](../RandomSteamGame.Client/Components/SteamPickerForm.razor#L160-L234), [result state](../RandomSteamGame.Client/Components/RandomGamePicker.razor#L133-L222) |
| Browser caches/history/downloads | Static Blazor/.NET resources can be cached by the browser. Result URLs contain Steam IDs; API routes/queries can contain Steam IDs or vanity names. CSV downloads remain user-controlled files; clearing cookies does not delete them. [Navigation](../RandomSteamGame.Client/Components/SteamPickerForm.razor#L290-L316), [API routes](../RandomSteamGame/Controllers/GameController.cs) |
| Other application stores | No active application use of sessionStorage, IndexedDB, a service worker or app-managed Cache Storage found. Do not equate this source search with a complete inventory of framework/browser behavior or production-injected resources |

No application authentication/session middleware or login cookie is configured.
Interactive Server circuits hold state in memory; they do not establish a persistent
account session. Data Protection is server cryptographic key storage, not another
browser cookie. Its configured/default key directory persists across requests;
Windows uses DPAPI, while non-Windows filesystem protection is operationally managed.
Losing keys can prevent browsers from managing old votes. See
[registration/key storage](../RandomSteamGame/Extensions/ServiceExtensions.cs#L152-L205).

## Server persistence, retention and cleanup

The application database is `Data/kgivler_com.db` beneath the binary directory.
Steam cache storage is separately configured by `Steam:ConnectionString`, normally
`Data/steam_cache.db`; voting uses the application DB. Actual paths/overrides and
backup locations require deployment confirmation. See
[application initialization](../RandomSteamGame/Extensions/ServiceExtensions.cs#L39-L60)
and [cache registration](../SteamApiClient/SteamApiDependencyInjection.cs#L31-L49).

| Store | Fields / association | Retention and implemented deletion |
| --- | --- | --- |
| `Visitors` | Raw `IpAddress` primary key, `Hits`, UTC `LastSeen` | **Indefinite under current application behavior:** no expiry or cleanup. Each homepage hit increments Hits and updates LastSeen. SUM(Hits) supplies visits; COUNT(IP records) estimates unique visitors, not unique people |
| `AppStats` | Singleton `Id`, `RandomGamesGenerated`, `LibrariesExported` | Aggregate counters without per-user fields; no expiry/reset UI |
| `RecentPicks` | `AppId`, sanitized `GameName`, UTC `PickedAtUtc`; no Steam/IP/browser identity fields | Each insertion prunes to newest 20 by timestamp/rowid; homepage shows 5. Count-bound, **not age-bound**; a quiet site's records may remain indefinitely |
| `FeatureProposals` | Stable ID/unique slug, title, description, status, optional GitHub issue number | No expiry; manual curation. Startup does not seed proposals |
| `FeatureVotes` | Proposal FK, 64-character SHA-256 `BrowserIdentityHash`, UTC creation timestamp; composite uniqueness | No expiry/cleanup. Open-proposal withdrawal deletes matching row. All other statuses reject additions **and withdrawals**, retaining historical totals/selections. Repeated support does not renew creation time |
| `CacheEntries` | Key `Id`, serialized `Value`, `ExpiresAtTimeUtc`, `SlidingExpirationInSeconds`, `AbsoluteExpirationUtc`; keys can contain Steam IDs/vanity names | Absolute periods below govern reuse. Pinned SQLite cache deletes an expired entry when read and sweeps expired rows after writes; **no autonomous sweep timer**. Inactive DBs can retain expired rows |

Evidence: [statistics call/query](../RandomSteamGame/Services/AppStatsService.cs#L42-L78),
[pinned visitor counter SQL](https://github.com/JoyfulReaper/JoyfulReaperLib/blob/dbb50609b4659103c16bcf2234946e9e6cb7d266/JoyfulReaperLib.WebStats.Sqlite/SqliteHitCounter.cs#L50-L95),
[recent writes/pruning](../RandomSteamGame/Services/SqliteRecentPickService.cs#L12-L44),
[voting reads/writes](../RandomSteamGame/Services/SqliteFeatureVotingService.cs#L43-L113), and
[pinned SQLite cache](https://github.com/JoyfulReaper/JoyfulReaperLib/blob/c0208000d34f1a843efe59fa6c4e239282f4a579/JoyfulReaperLib.Caching.Sqlite/SqliteDistributedCache.cs#L53-L154).
Dependency refs match package metadata for WebStats.Sqlite/Caching.Sqlite 0.0.10;
recheck implementation when upgrading them.

### Steam cache and CSV details

| Cache category | Tracked absolute period | Identifying content |
| --- | --- | --- |
| Owned games | 43,200 minutes / 30 days | Steam-ID key; public library and play activity |
| App details | 43,200 minutes / 30 days | Public game metadata, descriptions and resource URLs; game-ID key |
| App-details not found | 15 minutes | Negative result by game ID |
| Vanity success | 21,600 minutes / 15 days | Normalized vanity-name key and resolved Steam ID |
| Vanity not found | 15 minutes | Attempted normalized vanity-name key and negative result |
| Deck compatibility | 1,440 minutes / 1 day | Game-ID key and category |
| Owned-library refresh cooldown | 12 hours in server cache | Steam-ID key and reset timestamp |

These are configuration-dependent periods from [tracked cache settings](../RandomSteamGame/appsettings.json#L8-L17),
except the [12-hour tracker](../RandomSteamGame/Services/OwnedGamesCacheResetTracker.cs).
The [cache wrapper](../SteamApiClient/Services/CacheService.cs) sets policy expiry
and a five-minute local memory-cache period; do not substitute the registration's
generic 24-hour/30-minute defaults for these operation-specific policies.

Refresh uses HybridCache tag invalidation; this prevents reuse, not guaranteed
physical deletion. [Application invalidation](../SteamApiClient/Services/CacheService.cs#L126-L129)
and [HybridCache semantics](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-10.0#remove-cache-entries-by-tag)
do not support promising immediate erasure. SQL deletion also does not establish
secure erasure from database pages, journals, storage snapshots or backups.

Steam retrieval reads public library and Store/compatibility information; no Steam
account modification or password collection flow was found. CSV creation builds
UTF-8 bytes in server memory; the browser downloads `steam-library-{steamId}.csv`
through a temporary Blob URL which is revoked. No server export-file persistence
is implemented, but the underlying library cache remains. Export responses are
private/no-store. See [CSV builder](../RandomSteamGame/Services/SteamLibraryExportService.cs),
[export endpoint](../RandomSteamGame/Controllers/GameController.cs#L89-L229) and
[browser helper](../RandomSteamGame.Client/wwwroot/js/libraryExport.js).

## IP handling and temporary in-memory state

Keep three distinct uses of IP information separate:

1. **Persistent homepage statistics:** raw effective IP records remain indefinitely
   and produce visit counts/estimated unique visitors. Disabling telemetry or
   clearing browser cookies does not disable/delete these records.
2. **Temporary rate limiting:** voting computes a domain-separated HMAC-SHA256
   partition using a random process-lifetime key. IPv4 is normalized; IPv6 interfaces
   share a `/64`. Voting limiter keys and browser-hash partitions stay in process
   memory, with framework idle reclamation; no voting IP fields or voting telemetry
   are added. Default windows are 60 seconds; this is **not** an exact erasure deadline
   for an idle limiter object. Limits/keys reset on process restart.
3. **Telemetry/diagnostics:** a separate optional configured key HMACs IP bytes for
   telemetry. With a stable key, events can remain linkable; missing key omits the
   visitor ID. IP addresses may also appear in operational diagnostics/access logs;
   actual fields and retention are unknown.

Per-IP export admission uses an in-memory raw IPv4 or normalized IPv6 `/64` key;
successful exports have a 72-hour memory-cache cooldown. Configured Global mode
instead holds a single cooldown timestamp (tracked default 20 minutes) and admits
one active export. These are process-local, not durable IP history. Per-Steam-ID
refresh synchronization also uses a transient dictionary and removes entries after
the final lease is released. Ordinary request/circuit state and L1 caches live in
memory, without a guarantee of immediate physical memory erasure.

Sources: [voting signal](../RandomSteamGame/Services/FeatureVotingAbuseSignal.cs),
[voting limits](../RandomSteamGame/Services/FeatureVotingRateLimits.cs),
[telemetry ID](../RandomSteamGame/Services/VisitorIdProvider.cs),
[export IP partition](../RandomSteamGame/Services/LibraryExportRateLimitPartitionKey.cs),
[export cooldown](../RandomSteamGame/Services/LibraryExportCooldownTracker.cs),
[global cooldown](../RandomSteamGame/Services/GlobalLibraryExportCooldownTracker.cs), and
[refresh synchronization](../RandomSteamGame/Services/OwnedGamesRefreshAdmissionCoordinator.cs).

Voting is pseudonymous browser support, **not complete anonymity or verified
one-person-one-vote**. One hash links a browser's selections across proposals.
Cookie deletion/expiry, another browser/host or lost protection keys can produce a
new identity while old votes remain. No Steam/IP join is implemented in voting;
that does not prove correlation is impossible across separate operational records.

## Application logs and Mission Control

Mission Control is registered server-side; tracked settings disable delivery.
Enablement, destination and identifier key are deployment-dependent. When enabled,
the [client](https://github.com/JoyfulReaper/JoyfulReaperLib/blob/19061e93ac6d8f8f05f9adf2b9474aa04165d8b1/JoyfulReaperLib.MissionControl/MissionControlClient.cs#L26-L88)
(pinned package 0.0.3) POSTs event envelopes with event ID, timestamp, optional
correlation ID and payload. Failures are best-effort; no durable local event outbox
or receiver retention/deletion schedule is defined by this source. The operator
confirms Public delivery, indefinite archive retention and disabled I2P delivery
as described above; other receiver operations still require confirmation.

| Event | Relevant payload fields |
| --- | --- |
| [Site visit](../RandomSteamGame/Events/SiteVisitRecordedEvent.cs) | Optional IP-derived VisitorId, user agent (trimmed/truncated to 512 characters by AppStatsService), ingress label, estimated uniqueness, aggregate counts, duration |
| [Game pick](../RandomSteamGame/Events/GamePickCompletedEvent.cs) | Optional VisitorId, provider/ingress, game ID/sanitized name, unplayed preference, cache status/age, eligible count/library-size bucket, timings, outcome, commit |
| [Export completed](../RandomSteamGame/Events/LibraryExportCompletedEvent.cs) | Optional VisitorId, provider/ingress, library game count, compatibility counts, duration, commit |
| [Export rejected](../RandomSteamGame/Events/LibraryExportRejectedEvent.cs) | Optional VisitorId, provider/ingress, rejection reason, retry interval, commit |
| [Startup](../RandomSteamGame/Events/ApplicationStartedEvent.cs) | Environment, commit, deployment type, framework version; no visitor fields |

These event contracts have no Steam-ID, complete-library or voter-identifier fields,
and no feature-vote event was found. This does not mean logs are free of identifiers:
[picker errors](../RandomSteamGame.Client/Components/RandomGamePicker.razor#L276)
explicitly log Steam ID, while [API failures](../RandomSteamGame.Client/Services/RandomSteamApiClient.cs#L98-L129)
log request URIs that can contain IDs/vanity names. Client-side errors may reach
the browser console; Interactive Server component logs may reach server sinks.
The Mission Control dependency logs rejected response bodies; identifying content
could enter logs if the receiver echoes it (a possibility, not an observed leak).

Tracked logging uses Information by default and Warning for Microsoft.AspNetCore;
no dedicated HTTP-body logging middleware was found. Hosting/proxy access logs may
still record IPs, paths, queries, user agents or other headers. No application log,
Mission Control or infrastructure retention period should be inferred from these
settings; the indefinite Mission Control archive retention above is operator-confirmed.
Separate IRC/Matrix clients also handle nicknames/senders and messages
and print protocol/message content; they are not automatic website recipients.
If the policy covers those integrations, review their operations separately:
[IRC](../RandomSteamGame.IrcBot/IrcClient.cs#L156),
[Matrix](../RandomSteamGame.MatrixBot/Bot/MatrixBot.cs#L165).

## Third parties and external requests

| Destination | When contacted | Data-processing boundary |
| --- | --- | --- |
| Valve Steam Web API / Steam Store API | Backend requests on library, resolution or metadata operations; cache hits can avoid them | Steam ID, vanity name or game IDs go to Valve from the backend using application credentials; code does not forward browser identity cookies/IP/user-agent headers |
| Steam/CDN artwork and resources in descriptions | Automatic browser requests while rendering games when remote assets are allowed (Public default) | Remote hosts receive the browser's ordinary request metadata, including network address. Sanitized description HTML is not necessarily free of external resource loads; actual domains/headers/cookies vary with content/browser |
| Mission Control | Backend events; operator confirms Public delivery and disabled I2P delivery | Structured payloads above; archive currently retained indefinitely per operator; storage/access and other operations remain unverified |
| Beta picker | Backend availability GET to beta `/api/stats` when probing is enabled; result cached for 30 seconds | No visitor identifiers explicitly forwarded by the probe |
| Ko-fi / PayPal | User clicks donation link | Ordinary navigation, no payment SDK/iframe/form processing here. Provider processing and records received by Kyle require separate confirmation |
| GitHub | User clicks source/issues/suggestion links | Issue text/account handling occurs on GitHub, not through an on-site suggestion form |
| Developer portfolio/contact, chat/Matrix, affiliate/network links | User clicks links | External site/service handles interaction. Sponsorship contact/agreement precedes payment; donation links do not buy a feature |
| Hosting/CDN/proxy | Incoming website traffic | Repository describes GreenCloud hosting and supports Cloudflare/trusted proxies; actual providers, log fields, security cookies and topology require confirmation |

Sources: [Steam HTTP setup](../SteamApiClient/SteamApiDependencyInjection.cs#L53-L66),
[metadata output](../RandomSteamGame/Services/SteamProvider.cs#L227-L238),
[sanitization](../RandomSteamGame/Services/HtmlSanitizerService.cs),
[image loads](../RandomSteamGame.Client/Components/RandomGamePicker.razor.js#L119-L125),
[beta probe](../RandomSteamGame/Services/BetaAvailabilityService.cs),
[support](../RandomSteamGame/Components/Pages/Support.razor),
[About](../RandomSteamGame/Components/Pages/About.razor), and
[footer](../RandomSteamGame/Components/Layout/MainLayout.razor).

The [app shell](../RandomSteamGame/Components/App.razor) serves Bootstrap, styles,
scripts and icons locally. No advertising SDK, external font service or general
third-party analytics script was found in application code. A link alone does not
establish an automatic request to its destination. Production-injected resources
and cookies cannot be ruled out from source inspection.

## Public versus AltNet

| Behavior | Public | AltNet (DN42, I2P, Yggdrasil) |
| --- | --- | --- |
| Voting | Board/API enabled | Board omitted; GET/POST 404 before voter cookies, antiforgery/admission and request-time voting persistence activation; common startup still creates schema |
| Remote browser assets / beta probe | Allowed/enabled by default | Disabled by default; explicit capability overrides possible |
| Backend Steam requests / homepage IP records | Implemented | Still implemented; AltNet does not disable them |
| Forwarded headers | Enabled by default for configured trusted immediate proxies | Ignored by default; explicitly enabling requires trustworthy ingress |
| Server cookie security | Secure; development antiforgery exception above | Follows configured HTTP/HTTPS canonical origin; browser-written cookies follow actual browser protocol |

[ApplicationOptions](../RandomSteamGame/Options/ApplicationOptions.cs) and
[middleware](../RandomSteamGame/Extensions/MiddlewareExtensions.cs#L39-L102) select
capabilities and effective IP/scheme. Forwarding accepts one hop from exact trusted
peers (default loopback), strips untrusted forwarding headers, and only consumes
Cloudflare scheme metadata through trusted ingress. A trusted proxy must overwrite
visitor-supplied headers; trust in a peer does not prove it does so. Without reliable
client forwarding, the effective IP can be a shared proxy/tunnel address.

**NetworkMode is per process, not per hostname/network request.** Multiple ingress
paths into a Public instance inherit its Public behavior. CORS, canonical origin,
network labels and host-derived telemetry do not prove network isolation. Live
DN42/I2P/Yggdrasil routing, shared databases/keys and proxy logs are unknown.
See the [existing deployment boundary](anonymous-feature-voting.md#deployment-and-verification).

## Existing deletion choices and limits

- Users can reset blocked games, overwrite remembered input/preferences, clear site
  cookies/storage, and delete downloads. Identity-clear service methods exist, but
  no visible forget-identity UI was found. Cookie removal does not erase server data.
- Users can withdraw a vote while its proposal is open and the original cookie is
  usable. No bulk-vote/identity deletion or expired-identity recovery API exists.
- Library refresh invalidates cached data for reuse; it is not an account deletion
  endpoint and does not erase visitor records, telemetry, backups or downloads.
- No self-service deletion endpoint for visitor statistics, cached identity data,
  diagnostic logs or Mission Control events was found. An operator-assisted request
  process and backup exceptions must be confirmed rather than presented as implemented.

## Operational questions

The production facts above are confirmed. Resolve the remaining questions before
adding or strengthening corresponding public policy claims.

| Priority question for Kyle | Why it matters |
| --- | --- |
| What revision, cache TTLs and other configuration are deployed? | Tracked defaults are not production evidence; Public/I2P Mission Control enablement is confirmed above |
| What retention is intended for raw-IP Visitors records and for votes after cookie expiry/non-open status? | Current code keeps these indefinitely; a shorter promise requires a separately authorized implementation/operational process |
| What do IIS, reverse proxy, CDN, hosting and application sinks log, who can access them, and for how long? | IPs, identifier-bearing URLs and possibly headers can exist beyond SQLite |
| What are Mission Control's access controls, backups and deletion procedures? | Its archive currently retains events indefinitely per the operator; the remaining receiver operations are not established here |
| What backup/snapshot retention and database/key-directory permissions/encryption are in use? | SQL deletion/expiry is not a guarantee of physical erasure or deletion from backups |
| Do alternative network ingress paths share a Public process, databases or key rings? | Determines actual voting/asset behavior and whether records can be correlated across deployments |
| Which contact channel and operator procedure handle deletion requests, including identity verification and backup handling? | No comprehensive self-service deletion flow is implemented |
| What data do portfolio contact forms, donation providers and sponsorship correspondence retain or send to Kyle? | External collection is not established by link markup |
| Are any security cookies, scripts, fonts, ads or resources injected in production? Where are services/data hosted? | Neither third-party absence nor data residency can be established solely from application code |

Do not publish claims of no IP collection, complete anonymity, browser-only storage,
guaranteed deletion at cache/cookie expiry, consent-exempt cookies, unconditional vote
withdrawal, no external sharing, or specific log/backup retention without further
evidence. Business statements such as no sale of data also require Kyle's confirmation.

## Review triggers for future integrations

These are future design considerations, **not implemented features or approved changes**.
Update this inventory before releasing any new processing:

| Proposed integration | Questions to resolve during design |
| --- | --- |
| Steam authentication / account-linked preferences | Which verified account claims, login/session cookies and server records are necessary? Define logout versus deletion, retention/recovery and ownership verification. Decide explicitly whether existing browser votes may ever link to accounts; avoid silently joining previously separate identities |
| Advertising | Inventory vendor requests, cookies/storage, IP/device/referrer data, profiling, recipients and retention. Assess applicable consent/privacy requirements and AltNet behavior before enabling scripts; functional-cookie assumptions do not extend automatically to advertising |
| Sponsorship/payment integrations | Preserve contact/agreement before sponsorship payment and distinguish donations. Inventory contact/payment/webhook/entitlement fields, processors, access, retention and deletion; keep payment credentials/details out of logs and client bundles |
| Additional game providers | Confirm a usable permitted API and the minimum identifiers, scopes/tokens and library data required. Namespace provider identity/cache keys, protect credentials server-side, document new recipients/assets and revisit retention, revocation/deletion and AltNet behavior |

When implementation or dependencies change, recheck this inventory against source
and operational evidence instead of treating it as a permanent privacy guarantee.
