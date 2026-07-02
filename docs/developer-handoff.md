# Developer Handoff

## High-Level Architecture

RandomSteamGame is a .NET 10 solution split across four main projects:

- `RandomSteamGame`: the ASP.NET Core host. It serves the Blazor Web App, exposes API controllers, wires dependency injection, rate limiting, CORS, middleware, and persistence.
- `RandomSteamGame.Client`: interactive Blazor components that run with `InteractiveAuto`, plus the browser-side API client and cookie-backed browser storage helpers.
- `RandomSteamGame.Shared`: shared contracts and small abstractions used by both host and client.
- `SteamApiClient`: the Steam integration layer. It owns Steam Web API calls, Steam Store calls, hybrid caching, and cache invalidation.

At runtime this is a single web app:

1. The host starts in [Program.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Program.cs) and [ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs).
2. Razor components are mapped from the server host, with the client assembly added for interactive components.
3. Controllers under [Controllers](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers) provide JSON APIs for library lookup, random game selection, cache refresh, CSV export, and stats.
4. Steam API access is isolated behind [SteamProvider.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamProvider.cs), which uses `ISteamClient` and `ISteamStoreClient` from the `SteamApiClient` project.

## Important Projects And Folders

- [RandomSteamGame](/C:/GitHub/RandomSteamGame/RandomSteamGame)
  Server host, controllers, middleware, DI, services, Razor pages/layout, app settings.
- [RandomSteamGame/Controllers](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers)
  API entry points. `GameController` is the core Steam endpoint surface.
- [RandomSteamGame/Services](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services)
  Server-side application logic. Important files:
  `SteamProvider.cs`, `GameProviderFactory.cs`, `OwnedGamesCacheResetTracker.cs`, `AppStatsService.cs`, `SteamLibraryExportService.cs`.
- [RandomSteamGame/Extensions](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions)
  Startup composition and middleware pipeline.
- [RandomSteamGame/Components](/C:/GitHub/RandomSteamGame/RandomSteamGame/Components)
  Server-side routed pages and layouts. These pages host interactive client components.
- [RandomSteamGame.Client/Components](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components)
  The actual interactive picker UI and stats UI.
- [RandomSteamGame.Client/Services](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services)
  Browser-facing API wrapper and cookie-based browser identity store.
- [RandomSteamGame.Shared/Contracts](/C:/GitHub/RandomSteamGame/RandomSteamGame.Shared/Contracts)
  Shared DTOs like `OwnedGamesResponse`, `RandomGameResponse`, `GameDetails`, `SteamIdentity`.
- [SteamApiClient](/C:/GitHub/RandomSteamGame/SteamApiClient)
  Steam Web API and Steam Store client implementation, plus cache abstractions and SQLite-backed distributed cache.
- [RandomSteamGame.Tests](/C:/GitHub/RandomSteamGame/RandomSteamGame.Tests)
  Fast unit-style tests around controllers, provider behavior, browser identity helpers, and CSV export.
- [docs](/C:/GitHub/RandomSteamGame/docs)
  Non-code docs and screenshots. This handoff doc lives here.

## Request Flow For The Random Game Picker

### Home Page Flow

1. `/` renders [Home.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame/Components/Pages/Home.razor).
2. `Home.razor` hosts [SteamPickerForm.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/SteamPickerForm.razor) with `@rendermode="InteractiveAuto"`.
3. `SteamPickerForm` loads any existing identity from server cookies first through `ISteamIdentityReader`, then hydrates from browser cookies after interactive render via `BrowserSteamIdentityStore`.
4. On submit:
   - input is normalized
   - vanity URLs are reduced to the vanity slug if a full Steam Community URL was pasted
   - if only vanity is present, the client calls `GET /api/steam/resolve/{vanityUrl}`
   - once a numeric Steam ID is available, the browser stores it in cookies and navigates to `/random-game/steam/{steamId}` with optional `?unplayedOnly=true`

### Random Picker Flow

1. `/random-game/{provider}/{steamId}` renders [RandomGame.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame/Components/Pages/RandomGame.razor).
2. `RandomGame.razor` hosts [RandomGamePicker.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/RandomGamePicker.razor) with `InteractiveAuto`.
3. `RandomGamePicker` calls `RandomSteamApiClient.GetRandomGameDetailsAsync`.
4. That hits [GameController.GetRandomGameDetails](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs).
5. `GameController` validates the provider and Steam ID/vanity input, resolves vanity if needed, and asks `GameProviderFactory` for the provider implementation.
6. `GameProviderFactory` returns [SteamProvider](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamProvider.cs) for `"steam"`.
7. `SteamProvider`:
   - fetches owned games from `ISteamClient.GetOwnedGames`
   - filters excluded game IDs from the `ExcludedGameIds` cookie
   - optionally filters to unplayed titles
   - shuffles/selects candidate app IDs via `GameSelectionHelper`
   - fetches store metadata via `ISteamStoreClient.GetAppData`
   - sanitizes HTML descriptions
   - returns `GameDetails`
8. The controller increments the app stats counter on success via `IAppStatsService`, but that stat write is intentionally non-blocking.
9. `RandomGamePicker` renders the result, updates the adaptive theme JS module, and can add the chosen app ID to the blocked-game cookie list.

## Cache Strategy

There are a few separate caching layers in play.

### Steam Data Cache

Implemented in `SteamApiClient`:

- [SteamApiDependencyInjection.cs](/C:/GitHub/RandomSteamGame/SteamApiClient/SteamApiDependencyInjection.cs) configures `HybridCache` with:
  - L1 in-memory cache
  - L2 SQLite distributed cache through `SqliteDistributedCache`
- [CacheService.cs](/C:/GitHub/RandomSteamGame/SteamApiClient/Services/CacheService.cs) wraps the hybrid cache.
- [SteamClient.cs](/C:/GitHub/RandomSteamGame/SteamApiClient/HttpClients/SteamClient.cs) caches:
  - owned games by Steam ID
  - vanity URL success resolutions
  - vanity URL not-found responses
- [SteamStoreClient.cs](/C:/GitHub/RandomSteamGame/SteamApiClient/HttpClients/SteamStoreClient.cs) caches app details by app ID.

Configured TTLs come from [appsettings.json](/C:/GitHub/RandomSteamGame/RandomSteamGame/appsettings.json):

- `OwnedGames`: 43200 minutes in production
- `AppDetails`: 43200 minutes in production
- `VanitySuccess`: 21600 minutes in production
- `VanityNotFound`: 15 minutes in production

Important implication: owned library data is intentionally very sticky in production. New purchases will not show up until cache expiry or explicit cache invalidation.

### Owned Games Cache Reset Cooldown

Separate from Steam data caching:

- [OwnedGamesCacheResetTracker.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/OwnedGamesCacheResetTracker.cs) stores a `last reset` timestamp in the shared cache for 12 hours.
- `POST /api/steam/{userId}/library/refresh` invalidates the Steam owned-games cache by Steam user tag and then records the cooldown timestamp.
- The browser also stores the reset timestamp in a cookie so the UI can warn the user before it makes another reset request.

### Component State Caching

Interactive components use `PersistentComponentState` to avoid unnecessary refetches during prerender-to-interactive transitions:

- `SteamPickerForm` persists form state.
- `RandomGamePicker` persists the fetched `GameDetails`.

### In-Memory Probe Cache

- [BetaAvailabilityService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/BetaAvailabilityService.cs) caches the beta-site health probe in memory for 30 seconds.

## Authentication, Identity, And Storage Strategy

### Current Authentication State

There is no real authentication yet.

- API endpoints are effectively public.
- [GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs) is marked `[AllowAnonymous]`.
- There is an `Errors.Authentication` file, but it is not wired into any login flow today.

### Identity Model Today

The app currently uses Steam identifier persistence, not user accounts.

- Shared identity contract: [SteamIdentity.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Shared/Contracts/SteamIdentity.cs)
- Cookie names: [SteamIdentityCookies.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Shared/Contracts/SteamIdentityCookies.cs)

Server-side identity access:

- [ServerSteamIdentityReader.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityReader.cs)
- [ServerSteamIdentityWriter.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityWriter.cs)

Browser-side identity access:

- [BrowserSteamIdentityStore.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs)
- [cookieHelper.js](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/wwwroot/js/cookieHelper.js)

### What Is Stored

Cookies currently hold:

- `SteamId`
- `VanityUrl`
- `UnplayedOnly`
- `ExcludedGameIds`
- `OwnedGamesCacheResetAt`

SQLite currently holds:

- app stats table `AppStats`
- distributed Steam cache data managed by `SteamApiClient`
- hit counting data through `JoyfulReaperLib` helpers

Important implication: blocked games and remembered Steam IDs are browser-local preferences, not server-owned user data.

## Blazor Render Mode Rules

This app uses the new Blazor Web App model with both server and WebAssembly render modes enabled in [Program.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Program.cs):

- `AddInteractiveServerRenderMode()`
- `AddInteractiveWebAssemblyRenderMode()`

The pages in the host project are mostly static route shells.
The interactive behavior lives in client components and is usually attached with `InteractiveAuto`.

Current practical rules:

1. Put routable page shells in [RandomSteamGame/Components/Pages](/C:/GitHub/RandomSteamGame/RandomSteamGame/Components/Pages).
2. Put richer interactive UI in [RandomSteamGame.Client/Components](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components).
3. Use `@rendermode="InteractiveAuto"` on the host page when the child component needs interactivity.
4. Expect a prerender pass first, then an interactive pass.
5. Anything that requires browser-only APIs or JS should run only after interactivity is established.

Repo examples:

- `SteamPickerForm` checks `RendererInfo.IsInteractive` before browser-only hydration.
- `HomeStats` intentionally records a hit during non-interactive render, then only fetches stats during interactive render.
- `RandomGamePicker` uses `PersistentComponentState` so the prerendered result can survive into the interactive phase without an immediate duplicate fetch.

Good rule of thumb: if code touches `IJSRuntime`, browser cookies, or client-only state, treat it as post-interactive work unless there is a safe server fallback.

## Known Production Risks

These are the main risks I would keep in mind.

- No authentication or entitlement enforcement exists yet, so any premium feature added at the controller level will be publicly callable until auth and checks are introduced.
- Forwarded-header trust is narrow and a little unusual: [MiddlewareExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/MiddlewareExtensions.cs) clears known networks/proxies and manually rewrites `X-Forwarded-Proto` from `CF-Visitor`. This is functional but easy to misconfigure if the deployment topology changes.
- Steam cache durations are very long in production. That helps performance but can make the product feel stale when a user buys or refunds games.
- The app uses SQLite for both cache and app stats. That is simple and cheap, but it can become a scaling bottleneck under higher concurrency or on shared/networked storage.
- Rate limiting is fixed-window and global by policy name. It helps, but it is not tied to authenticated users yet, so one noisy source can still affect anonymous traffic patterns.
- Blocked games and some UX state live only in browser cookies. Users switching browsers/devices will lose that state.
- There is no dedicated observability layer beyond app logs, problem details, and the stats counters. Production diagnosis may be slower than ideal if Steam starts failing intermittently.
- `RandomGamePicker` clears current result state before each fetch. If an API call fails after a successful prior result, the user loses the previous card instead of keeping stale data on screen.
- Premium work will need careful placement because the current UI and APIs assume anonymous flows and cookie-based identity, not a real user account.

## Common Debugging Steps

### Startup And Configuration

1. Check [appsettings.Development.json](/C:/GitHub/RandomSteamGame/RandomSteamGame/appsettings.Development.json) for `Steam:ApiKey`, cache durations, and rate-limit settings.
2. If startup fails immediately, inspect [ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs) and `SteamApiDependencyInjection.cs`. Both validate Steam configuration and can throw on invalid API key setup.
3. Confirm the SQLite cache path from `Steam:ConnectionString`. Relative paths are rewritten under the app base directory `Data` folder by `SteamApiClient`.

### API Flow

1. Start at [GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs).
2. Confirm validation first:
   - Steam IDs must be 17 digits within the hardcoded min/max range.
   - Vanity URLs must match the regex.
3. Confirm the provider key is `"steam"`.
4. Step into [SteamProvider.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamProvider.cs) next.
5. If the picker returns not found or empty-library behavior, inspect what `ISteamClient.GetOwnedGames` returned and whether excluded IDs removed the whole selectable set.

### Cache Problems

1. If a user says their library is stale, verify whether they used `POST /api/steam/{steamId}/library/refresh`.
2. If refresh appears blocked, inspect `OwnedGamesCacheResetTracker` and the `OwnedGamesCacheResetAt` browser cookie.
3. If app metadata seems stale or missing, check `SteamStoreClient.GetAppData` and the app-details cache entry.
4. If invalidation seems ineffective, remember that owned-games entries are invalidated by the `steam_user_{steamId}` tag.

### Browser State Problems

1. Inspect cookies:
   - `SteamId`
   - `VanityUrl`
   - `UnplayedOnly`
   - `ExcludedGameIds`
   - `OwnedGamesCacheResetAt`
2. If the UI behaves differently on first render vs after hydration, look for `RendererInfo.IsInteractive` guards and `PersistentComponentState` restores.
3. If the adaptive background fails, check the JS import in `RandomGamePicker.razor.js` and browser console logs.

### Rate Limit Problems

1. Inspect the `steam_api_limiter` policy in [ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs).
2. Check whether the failing endpoint is under `GameController` or `HitController`, since both use that policy.
3. In development, `PermitLimit` is lower than production.

### Useful Commands

Run these from the repo root:

```powershell
dotnet build RandomSteamGame.slnx
dotnet test RandomSteamGame.slnx
```

## Next Recommended Features

- Add real authentication and entitlement checks before more premium endpoints are exposed.
- Introduce a server-owned user profile model so blocked games, export history, and preferences are not browser-only.
- Add a premium gate abstraction around export and future advanced filters, instead of sprinkling auth checks directly into controllers.
- Add richer filtering:
  playtime ranges, tags/genres, installed-only, Steam Deck verified/playable filters, multiplayer/single-player filters.
- Add observability:
  structured request logging, cache hit/miss metrics, Steam API failure counters.
- Add integration tests around controller endpoints and common failure cases, especially rate-limited behavior and Steam API outage paths.
- Add a client method for CSV export if a UI download button is eventually desired.
- Revisit cache TTLs and make local cache expiration configurable instead of hardcoded.

## Files Most Likely To Need Changes For Premium Features

If premium work starts next, these files are the most likely touch points:

- [RandomSteamGame/Controllers/GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs)
  Entry point for gating library export and any future premium-only endpoints.
- [RandomSteamGame/Services/Interfaces/ISteamLibraryExportService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/Interfaces/ISteamLibraryExportService.cs)
  Good seam for moving export behind entitlement checks.
- [RandomSteamGame/Services/SteamLibraryExportService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamLibraryExportService.cs)
  Export formatting stays here even if access control moves elsewhere.
- [RandomSteamGame/Extensions/ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs)
  DI registration point for auth services, premium policy services, and new storage dependencies.
- [RandomSteamGame/Extensions/MiddlewareExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/MiddlewareExtensions.cs)
  Where auth middleware and any claims-based pipeline changes would be added.
- [RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs)
  Likely needs to coexist with or yield to authenticated user state.
- [RandomSteamGame.Shared/Contracts/SteamIdentity.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Shared/Contracts/SteamIdentity.cs)
  May need expansion or separation once user accounts exist.
- [RandomSteamGame/Services/ServerSteamIdentityReader.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityReader.cs)
  Likely to change if Steam identity becomes user-profile backed instead of cookie-only.
- [RandomSteamGame/Services/ServerSteamIdentityWriter.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityWriter.cs)
  Same reason as above.
- [RandomSteamGame.Client/Services/RandomSteamApiClient.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/RandomSteamApiClient.cs)
  Where authenticated export/download helpers or auth-aware request behavior would likely be added.
- [RandomSteamGame.Client/Components/SteamPickerForm.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/SteamPickerForm.razor)
  If premium gating starts surfacing in the UX.
- [RandomSteamGame.Client/Components/RandomGamePicker.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/RandomGamePicker.razor)
  Likely place for premium-only filters, blocked-game persistence changes, or export affordances.
- [RandomSteamGame/Common/Errors/Errors.Authentication.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Common/Errors/Errors.Authentication.cs)
  A natural place to expand auth-related error definitions once they are real.

## Short Mental Model

If you only remember one thing, remember this:

The host project owns routing, API, middleware, and server services.
The client project owns interactive UI and browser cookie state.
The SteamApiClient project owns all external Steam calls and caching.
`GameController -> GameProviderFactory -> SteamProvider -> SteamApiClient` is the core request path.
