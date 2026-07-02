# Premium Feature Issues

This document breaks premium work into GitHub issue-style tasks based on the current repository structure.

## Issue 1: Sign In With Steam

### Goal

Add real user authentication via Steam OpenID so premium features can be tied to a stable user account instead of browser-only cookies.

### Affected Files And Services

- [RandomSteamGame/Program.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Program.cs)
- [RandomSteamGame/Extensions/ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs)
- [RandomSteamGame/Extensions/MiddlewareExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/MiddlewareExtensions.cs)
- [RandomSteamGame/Common/Errors/Errors.Authentication.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Common/Errors/Errors.Authentication.cs)
- [RandomSteamGame/Controllers/GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs)
- New auth-focused controller, likely `AuthController`
- [RandomSteamGame/Services/ServerSteamIdentityReader.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityReader.cs)
- [RandomSteamGame/Services/ServerSteamIdentityWriter.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityWriter.cs)
- [RandomSteamGame.Client/Components/Layout/NavMenu.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/Layout/NavMenu.razor)
- [RandomSteamGame.Client/Components/SteamPickerForm.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/SteamPickerForm.razor)
- [RandomSteamGame.Shared/Contracts/SteamIdentity.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Shared/Contracts/SteamIdentity.cs)

### Suggested Data Model

- `User`
  - `Id` integer primary key
  - `SteamId` text unique
  - `DisplayName` text nullable
  - `AvatarUrl` text nullable
  - `CreatedUtc` datetime
  - `LastSeenUtc` datetime
  - `PremiumTier` text or integer
  - `PremiumExpiresUtc` datetime nullable

- `UserSession` only if a custom session store becomes necessary later
  - likely not needed initially if cookie auth is enough

### API Endpoints

- `GET /auth/steam/sign-in`
- `GET /auth/steam/callback`
- `POST /auth/sign-out`
- `GET /api/auth/me`

### UI Changes

- Add sign-in/sign-out controls in the nav.
- Show current signed-in Steam identity.
- Keep the existing anonymous picker flow working until premium gates are turned on.
- Add small premium-state UI messaging where relevant.

### Tests

- Auth controller tests for challenge/callback behavior.
- Tests that authenticated identity is surfaced correctly from `GET /api/auth/me`.
- Tests for anonymous vs authenticated access on premium endpoints once gating is added.
- Middleware/integration tests for auth cookie behavior.

### Risks

- Steam sign-in is identity-only, not entitlement/payment. Premium authorization still needs its own rules.
- Mixing current cookie-based Steam ID persistence with authenticated user identity can create confusing precedence bugs.
- Auth changes can affect prerender vs interactive behavior if identity is fetched inconsistently.

## Issue 2: SQLite-Backed Permanent Blocked Games

### Goal

Move blocked games from browser cookies into persistent server-owned storage tied to the authenticated user.

### Affected Files And Services

- [RandomSteamGame.Client/Components/RandomGamePicker.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/RandomGamePicker.razor)
- [RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs)
- [RandomSteamGame/Services/SteamProvider.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamProvider.cs)
- [RandomSteamGame/Services/GameProviderFactory.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/GameProviderFactory.cs)
- [RandomSteamGame/Extensions/ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs)
- [RandomSteamGame/Services/AppStatsService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/AppStatsService.cs)
- New repository/service pair, likely `IBlockedGamesService` and `BlockedGamesService`
- Potential new controller or endpoints added to `GameController`

### Suggested Data Model

- `BlockedGame`
  - `Id` integer primary key
  - `UserId` integer foreign key
  - `SteamAppId` integer
  - `GameName` text nullable
  - `CreatedUtc` datetime
  - unique index on `UserId + SteamAppId`

### API Endpoints

- `GET /api/me/blocked-games`
- `POST /api/me/blocked-games`
- `DELETE /api/me/blocked-games/{steamAppId}`
- `DELETE /api/me/blocked-games`

### UI Changes

- Update “Don’t choose this game again” to call the server instead of writing only to cookies.
- Add a small management view for blocked games, even if minimal.
- Keep a temporary fallback/migration path for existing `ExcludedGameIds` cookie values.

### Tests

- Service tests for add/remove/list/reset behavior.
- Controller tests for auth requirements and duplicate handling.
- Picker tests confirming blocked games are excluded from selection.
- Migration test or behavior test for first-time import from cookie state if implemented.

### Risks

- If the picker still reads browser cookies and DB at the same time, exclusion behavior can drift.
- Large blocked-game lists could slow selection if fetched poorly on every request.
- Migration from cookie-only state to server state needs a clear one-time strategy.

## Issue 3: Favorites

### Goal

Allow users to save favorite games for quick access and future premium features like favorite-only filtering or collections.

### Affected Files And Services

- [RandomSteamGame.Client/Components/RandomGamePicker.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/RandomGamePicker.razor)
- [RandomSteamGame/Controllers/GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs) or a new `FavoritesController`
- [RandomSteamGame/Extensions/ServiceExtensions.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Extensions/ServiceExtensions.cs)
- New `IFavoritesService` and `FavoritesService`
- Shared contracts in [RandomSteamGame.Shared/Contracts](/C:/GitHub/RandomSteamGame/RandomSteamGame.Shared/Contracts)

### Suggested Data Model

- `FavoriteGame`
  - `Id` integer primary key
  - `UserId` integer foreign key
  - `SteamAppId` integer
  - `GameName` text nullable
  - `HeaderImage` text nullable
  - `CreatedUtc` datetime
  - unique index on `UserId + SteamAppId`

### API Endpoints

- `GET /api/me/favorites`
- `POST /api/me/favorites`
- `DELETE /api/me/favorites/{steamAppId}`

### UI Changes

- Add “Favorite” / “Unfavorite” action on the random picker result.
- Add a favorites page or section showing saved games.
- Optionally add links from favorites back into the store or picker.

### Tests

- Service tests for idempotent add/remove.
- Controller tests for authenticated access and duplicate favorites.
- Component tests if a UI test setup is introduced later.

### Risks

- Duplicating lightweight game metadata in favorites is convenient but can go stale.
- Pulling full live game details for every favorite view may be too expensive.
- Favorites can overlap with history and blocked games, so cross-feature behavior needs clear rules.

## Issue 4: Filters

### Goal

Add premium filtering controls so the picker can narrow the candidate pool before selecting a random game.

### Affected Files And Services

- [RandomSteamGame.Client/Components/SteamPickerForm.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/SteamPickerForm.razor)
- [RandomSteamGame.Client/Components/RandomGamePicker.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/RandomGamePicker.razor)
- [RandomSteamGame.Client/Models/SteamPickerFormModel.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Models/SteamPickerFormModel.cs)
- [RandomSteamGame.Client/Services/RandomSteamApiClient.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/RandomSteamApiClient.cs)
- [RandomSteamGame/Controllers/GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/SteamProvider.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamProvider.cs)
- [RandomSteamGame/Services/GameSelectionHelper.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/GameSelectionHelper.cs)
- Shared request/response contracts

### Suggested Data Model

Start with request-level filters, then persist preferences later:

- `PickerFilters`
  - `UnplayedOnly` bool
  - `MinPlaytimeMinutes` int nullable
  - `MaxPlaytimeMinutes` int nullable
  - `FavoritesOnly` bool
  - `ExcludeBlocked` bool
  - `IncludeAppIds` list nullable
  - `ExcludeAppIds` list nullable
  - future: tags/genres/platform/deck support

If saved filters are needed:

- `UserSavedFilterPreset`
  - `Id`
  - `UserId`
  - `Name`
  - serialized filter fields

### API Endpoints

- Extend:
  - `GET /api/steam/random-game`
  - `GET /api/steam/random-game/details`
- Optional future:
  - `GET /api/me/filter-presets`
  - `POST /api/me/filter-presets`
  - `DELETE /api/me/filter-presets/{id}`

### UI Changes

- Expand the picker form with premium filter controls.
- Keep the initial filter set simple:
  unplayed, playtime range, favorites only, exclude blocked.
- Show active filters on the result page.
- Add validation messages for conflicting filter combinations.

### Tests

- Controller tests for query binding and validation.
- Service tests for each filter rule and combinations.
- Regression tests for empty result sets after filtering.
- Tests that premium-only filters are rejected or ignored for anonymous users, depending on product choice.

### Risks

- Too many filters can explode request complexity and create confusing UX.
- Some filters need more data than owned-games currently provides, which may force more Steam Store requests.
- Edge cases where filters remove every candidate need a clear problem response.

## Issue 5: Picker History

### Goal

Track a user’s past random picks so they can revisit what was suggested and support features like dedupe or analytics.

### Affected Files And Services

- [RandomSteamGame/Controllers/GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/SteamProvider.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamProvider.cs)
- [RandomSteamGame/Services/AppStatsService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/AppStatsService.cs)
- New `IPickerHistoryService` and `PickerHistoryService`
- [RandomSteamGame.Client/Components/RandomGamePicker.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/RandomGamePicker.razor)
- New page/component for history display

### Suggested Data Model

- `PickerHistory`
  - `Id` integer primary key
  - `UserId` integer foreign key
  - `SteamAppId` integer
  - `GameName` text nullable
  - `HeaderImage` text nullable
  - `Provider` text
  - `UnplayedOnly` bool
  - `PickedUtc` datetime
  - `WasFavoritedAtPick` bool nullable

Optional later:

- store filter snapshot JSON for better auditing

### API Endpoints

- `GET /api/me/picker-history`
- `GET /api/me/picker-history?skip=0&take=50`
- `DELETE /api/me/picker-history/{id}`
- Optional:
  `DELETE /api/me/picker-history`

### UI Changes

- Add a history page or drawer.
- Optionally add “pick again from history” behavior.
- Add basic pagination or infinite scroll if history grows.

### Tests

- Service tests for append/list/delete behavior.
- Controller tests for pagination and auth.
- Tests ensuring successful random picks are recorded exactly once.

### Risks

- Recording history in the request path can add latency if implemented synchronously.
- History can grow unbounded without retention or pagination.
- There is some overlap with favorites and blocked games, so interactions should be defined early.

## Issue 6: CSV Export

### Goal

Move the existing CSV export capability behind premium/auth checks and optionally add a client-side download entry point.

### Affected Files And Services

- [RandomSteamGame/Controllers/GameController.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/Interfaces/ISteamLibraryExportService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/Interfaces/ISteamLibraryExportService.cs)
- [RandomSteamGame/Services/SteamLibraryExportService.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/SteamLibraryExportService.cs)
- [RandomSteamGame.Client/Services/RandomSteamApiClient.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/RandomSteamApiClient.cs)
- [RandomSteamGame.Client/Components/SteamPickerForm.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/SteamPickerForm.razor) or a future account page
- [RandomSteamGame.Tests/SteamLibraryExportServiceTests.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Tests/SteamLibraryExportServiceTests.cs)

### Suggested Data Model

No required persistent model for the export itself.

Optional if tracking usage:

- `ExportAudit`
  - `Id`
  - `UserId`
  - `ExportType`
  - `RequestedUtc`
  - `RowCount`

### API Endpoints

- Existing endpoint to gate:
  `GET /api/steam/{steamId}/library/export.csv`
- Optional auth-owned variant:
  `GET /api/me/library/export.csv`

### UI Changes

- Add a premium-only export button.
- If auth becomes primary, prefer exporting the signed-in user’s library instead of requiring a raw Steam ID in the UI.
- Show friendly messaging when export is unavailable for anonymous users.

### Tests

- Controller tests for premium/auth enforcement.
- Existing CSV content tests should stay.
- If a `me` endpoint is added, tests for identity binding from the signed-in user.

### Risks

- Leaving the raw Steam ID export endpoint public defeats the premium gate.
- If both public and authenticated endpoints remain, behavior can diverge.
- Export volume may need tighter rate limiting than the random picker.

## Issue 7: User Preferences

### Goal

Persist user-specific picker settings and UX preferences on the server so the experience follows the user across browsers and devices.

### Affected Files And Services

- [RandomSteamGame.Client/Components/SteamPickerForm.razor](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Components/SteamPickerForm.razor)
- [RandomSteamGame.Client/Models/SteamPickerFormModel.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Models/SteamPickerFormModel.cs)
- [RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs)
- [RandomSteamGame/Services/ServerSteamIdentityReader.cs](/C:/GitHub/RandomSteamGame/RandomSteamGame/Services/ServerSteamIdentityReader.cs)
- New `IUserPreferencesService` and `UserPreferencesService`
- New controller for preferences
- Shared preference DTOs

### Suggested Data Model

- `UserPreferences`
  - `UserId` integer primary key / foreign key
  - `DefaultSteamId` text nullable
  - `DefaultUnplayedOnly` bool
  - `DefaultMinPlaytimeMinutes` int nullable
  - `DefaultMaxPlaytimeMinutes` int nullable
  - `PreferFavorites` bool
  - `Theme` text nullable
  - `UpdatedUtc` datetime

### API Endpoints

- `GET /api/me/preferences`
- `PUT /api/me/preferences`
- Optional:
  `POST /api/me/preferences/reset`

### UI Changes

- Add a preferences screen or collapsible account section.
- Load saved defaults into the picker form.
- Decide whether browser cookies remain as local overrides or are replaced by server preferences after sign-in.

### Tests

- Service tests for read/update behavior.
- Controller tests for validation and auth.
- UI behavior tests for applying defaults on load.
- Tests for preference precedence:
  query string vs saved preference vs cookie fallback.

### Risks

- Preference precedence can get confusing fast if cookies, query params, and DB values all coexist.
- Storing `DefaultSteamId` in user preferences needs product clarity if users can sign into one Steam account but query another.
- Preferences often become a dumping ground; keep the first version intentionally small.

## Suggested Delivery Order

1. Sign in with Steam
2. User preferences
3. SQLite-backed permanent blocked games
4. Favorites
5. Picker history
6. Filters
7. CSV export premium gating and UI

This order establishes identity first, then user-owned storage, then richer premium behavior on top of that foundation.
