# Developer Handoff

## High-Level Architecture

RandomSteamGame is a .NET 10 solution split across four main projects:

- `RandomSteamGame`: ASP.NET Core host, controllers, middleware, DI, persistence, and server-only telemetry.
- `RandomSteamGame.Client`: interactive Blazor components plus browser-side API and cookie helpers.
- `RandomSteamGame.Shared`: shared contracts and small abstractions used by both host and client.
- `SteamApiClient`: Steam Web API and Store API integration plus caching.

Runtime flow:

1. The host starts in [Program.cs](../RandomSteamGame/Program.cs) and [ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs).
2. Razor components are mapped from the server host and interactive components are attached through the client assembly.
3. Controllers under [Controllers](../RandomSteamGame/Controllers) provide APIs for library lookup, random game selection, cache refresh, CSV export, stats, and vanity resolution.
4. Steam integration is isolated behind [SteamProvider.cs](../RandomSteamGame/Services/SteamProvider.cs), which uses `ISteamClient` and `ISteamStoreClient`.

## Important Projects And Folders

- [RandomSteamGame](../RandomSteamGame)
  Server host, controllers, middleware, DI, services, Razor pages/layout, and app settings.
- [RandomSteamGame/Controllers](../RandomSteamGame/Controllers)
  API entry points. [GameController.cs](../RandomSteamGame/Controllers/GameController.cs) is the core Steam endpoint surface.
- [RandomSteamGame/Services](../RandomSteamGame/Services)
  Server-side application logic, including [SteamProvider.cs](../RandomSteamGame/Services/SteamProvider.cs), [GameProviderFactory.cs](../RandomSteamGame/Services/GameProviderFactory.cs), [OwnedGamesCacheResetTracker.cs](../RandomSteamGame/Services/OwnedGamesCacheResetTracker.cs), [AppStatsService.cs](../RandomSteamGame/Services/AppStatsService.cs), and [SteamLibraryExportService.cs](../RandomSteamGame/Services/SteamLibraryExportService.cs).
- [RandomSteamGame/Extensions](../RandomSteamGame/Extensions)
  Startup composition and middleware pipeline.
- [RandomSteamGame/Components](../RandomSteamGame/Components)
  Server-side routed pages and layouts.
- [RandomSteamGame.Client/Components](../RandomSteamGame.Client/Components)
  Interactive picker UI and stats UI.
- [RandomSteamGame.Client/Services](../RandomSteamGame.Client/Services)
  Browser-facing API wrapper and cookie-backed identity store.
- [RandomSteamGame.Shared/Contracts](../RandomSteamGame.Shared/Contracts)
  Shared DTOs like `OwnedGamesResponse`, `GameDetails`, and `SteamIdentity`.
- [SteamApiClient](../SteamApiClient)
  Steam Web API and Steam Store client implementation plus cache abstractions.
- [RandomSteamGame.Tests](../RandomSteamGame.Tests)
  Controller, provider, browser identity, SQLite initializer, and CSV export tests.
- [docs](.)
  Non-code docs and screenshots.

## Request Flow For The Random Game Picker

### Home Page Flow

1. `/` renders [Home.razor](../RandomSteamGame/Components/Pages/Home.razor).
2. `Home.razor` hosts [SteamPickerForm.razor](../RandomSteamGame.Client/Components/SteamPickerForm.razor) with `InteractiveAuto`.
3. `SteamPickerForm` loads any existing identity from server cookies first through `ISteamIdentityReader`, then hydrates from browser cookies after interactive render via `BrowserSteamIdentityStore`.
4. On submit:
   - input is normalized
   - vanity URLs are reduced to the vanity slug if a full Steam Community URL was pasted
   - if only vanity is present, the client calls `GET /api/steam/resolve/{vanityUrl}`
   - once a numeric Steam ID is available, the browser stores it in cookies and navigates to `/random-game/steam/{steamId}` with optional `?unplayedOnly=true`

### Random Picker Flow

1. `/random-game/{provider}/{steamId}` renders [RandomGame.razor](../RandomSteamGame/Components/Pages/RandomGame.razor).
2. `RandomGame.razor` hosts [RandomGamePicker.razor](../RandomSteamGame.Client/Components/RandomGamePicker.razor) with `InteractiveAuto`.
3. `RandomGamePicker` calls `RandomSteamApiClient.GetRandomGameDetailsAsync`.
4. That hits [GameController.GetRandomGameDetails](../RandomSteamGame/Controllers/GameController.cs).
5. `GameController` validates the provider and identifier input, resolves vanity if needed, and asks `GameProviderFactory` for the provider implementation.
6. `GameProviderFactory` returns [SteamProvider](../RandomSteamGame/Services/SteamProvider.cs) for `"steam"`.
7. `SteamProvider` fetches owned games, filters excluded game IDs from the `ExcludedGameIds` cookie, optionally filters to unplayed titles, shuffles candidate app IDs, fetches store metadata, sanitizes HTML descriptions, and returns `GameDetails`.
8. The controller increments the app stats counter on success via `IAppStatsService`. That write is intentionally non-blocking.
9. The controller publishes best-effort Mission Control telemetry for completed requests with outcome values such as `unsupported-provider`, `invalid-identifier`, `identifier-resolution-failed`, `selection-failed`, and `served`.
10. `RandomGamePicker` renders the result, updates the adaptive theme JS module, and can add the chosen app ID to the blocked-game cookie list.

## Cache Strategy

### Steam Data Cache

Implemented in `SteamApiClient`:

- [SteamApiDependencyInjection.cs](../SteamApiClient/SteamApiDependencyInjection.cs) configures `HybridCache` with L1 memory and L2 SQLite distributed caching.
- [CacheService.cs](../SteamApiClient/Services/CacheService.cs) wraps the hybrid cache.
- [SteamClient.cs](../SteamApiClient/HttpClients/SteamClient.cs) caches owned games by Steam ID and vanity URL resolution results.
- [SteamStoreClient.cs](../SteamApiClient/HttpClients/SteamStoreClient.cs) caches app details by app ID.

Configured TTLs come from [appsettings.json](../RandomSteamGame/appsettings.json).

Important implication: owned library data is intentionally sticky in production. New purchases will not show up until cache expiry or explicit cache invalidation.

### Owned Games Cache Reset Cooldown

- [OwnedGamesCacheResetTracker.cs](../RandomSteamGame/Services/OwnedGamesCacheResetTracker.cs) stores a `last reset` timestamp in the shared cache for 12 hours.
- `POST /api/steam/{userId}/library/refresh` invalidates the Steam owned-games cache by Steam user tag and then records the cooldown timestamp.
- The browser also stores the reset timestamp in a cookie so the UI can warn the user before it makes another reset request.

### Component State Caching

Interactive components use `PersistentComponentState` to avoid unnecessary refetches during prerender-to-interactive transitions:

- `SteamPickerForm` persists form state.
- `RandomGamePicker` persists the fetched `GameDetails`.

## Authentication, Identity, And Storage Strategy

### Current Authentication State

There is no real authentication yet.

- API endpoints are effectively public.
- [GameController.cs](../RandomSteamGame/Controllers/GameController.cs) is marked `[AllowAnonymous]`.
- `Errors.Authentication` exists, but it is not wired into any login flow today.

### Identity Model Today

The app currently uses Steam identifier persistence, not user accounts.

- Shared identity contract: [SteamIdentity.cs](../RandomSteamGame.Shared/Contracts/SteamIdentity.cs)
- Cookie names: [SteamIdentityCookies.cs](../RandomSteamGame.Shared/Contracts/SteamIdentityCookies.cs)

Server-side identity access:

- [ServerSteamIdentityReader.cs](../RandomSteamGame/Services/ServerSteamIdentityReader.cs)
- [ServerSteamIdentityWriter.cs](../RandomSteamGame/Services/ServerSteamIdentityWriter.cs)

Browser-side identity access:

- [BrowserSteamIdentityStore.cs](../RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs)
- [cookieHelper.js](../RandomSteamGame.Client/wwwroot/js/cookieHelper.js)

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

## Blazor Render Mode Rules

This app uses the new Blazor Web App model with both server and WebAssembly render modes enabled in [Program.cs](../RandomSteamGame/Program.cs).

Practical rules:

1. Put routable page shells in [RandomSteamGame/Components/Pages](../RandomSteamGame/Components/Pages).
2. Put richer interactive UI in [RandomSteamGame.Client/Components](../RandomSteamGame.Client/Components).
3. Use `@rendermode="InteractiveAuto"` on the host page when the child component needs interactivity.
4. Expect a prerender pass first, then an interactive pass.
5. Anything that requires browser-only APIs or JS should run only after interactivity is established.

## Observability And Telemetry

- Mission Control is registered on the server through the `MissionControl` configuration section in [ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs).
- The client project does not reference Mission Control, so credentials stay server-only.
- `GameController` emits a best-effort `randomsteam.game-pick.completed` event after successful picks and on meaningful failures.
- Telemetry failures are isolated from business responses and logged rather than propagated.

## Known Production Risks

- No authentication or entitlement enforcement exists yet, so any premium feature added at the controller level will be publicly callable until auth and checks are introduced.
- Forwarded-header trust is narrow and manually rewritten from `CF-Visitor`. That is functional but easy to misconfigure if the deployment topology changes.
- Steam cache durations are long in production. That helps performance but can make the product feel stale when a user buys or refunds games.
- SQLite is used for both cache and app stats. That is simple and cheap, but it can become a scaling bottleneck under higher concurrency or on shared storage.
- Blocked games and some UX state live only in browser cookies. Users switching browsers or devices will lose that state.
- Mission Control telemetry is best-effort by design. It should never block a successful picker response.

## Common Debugging Steps

### Startup And Configuration

1. Check [appsettings.Development.json](../RandomSteamGame/appsettings.Development.json) for `Steam:ApiKey`, cache durations, rate-limit settings, and the disabled Mission Control template.
2. If startup fails immediately, inspect [ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs) and `SteamApiDependencyInjection.cs`. Both validate Steam configuration and can throw on invalid API key setup.
3. Confirm the SQLite cache path from `Steam:ConnectionString`. Relative paths are rewritten under the app base directory `Data` folder by `SteamApiClient`.

### API Flow

1. Start at [GameController.cs](../RandomSteamGame/Controllers/GameController.cs).
2. Confirm validation first:
   - Steam IDs must be exactly 17 digits.
   - Vanity URLs must match the normalization rules in `SteamVanityUrlHelper`.
3. Confirm the provider key is `"steam"`.
4. Step into [SteamProvider.cs](../RandomSteamGame/Services/SteamProvider.cs) next.
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

1. Inspect the `steam_api_limiter` policy in [ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs).
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
- Add richer filtering: playtime ranges, tags/genres, installed-only, Steam Deck verified/playable filters, multiplayer/single-player filters.
- Add observability: structured request logging, cache hit/miss metrics, Steam API failure counters.
- Add integration tests around controller endpoints and common failure cases, especially rate-limited behavior and Steam API outage paths.

## Short Mental Model

If you only remember one thing, remember this:

The host project owns routing, API, middleware, and server services.
The client project owns interactive UI and browser cookie state.
The SteamApiClient project owns all external Steam calls and caching.
`GameController -> GameProviderFactory -> SteamProvider -> SteamApiClient` is the core request path.
