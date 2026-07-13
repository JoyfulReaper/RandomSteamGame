# Premium Feature Issues

This document tracks the next likely premium and account-related work. It is intentionally high level and references the current repository layout with relative links.

## Issue 1: Sign In With Steam

Goal: add Steam OpenID sign-in so premium features can be tied to a stable account.

Likely touch points:

- [RandomSteamGame/Program.cs](../RandomSteamGame/Program.cs)
- [RandomSteamGame/Extensions/ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs)
- [RandomSteamGame/Controllers/GameController.cs](../RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/ServerSteamIdentityReader.cs](../RandomSteamGame/Services/ServerSteamIdentityReader.cs)
- [RandomSteamGame/Services/ServerSteamIdentityWriter.cs](../RandomSteamGame/Services/ServerSteamIdentityWriter.cs)
- [RandomSteamGame.Client/Components/Layout/NavMenu.razor](../RandomSteamGame.Client/Components/Layout/NavMenu.razor)

## Issue 2: SQLite-Backed Permanent Blocked Games

Goal: move blocked games out of browser cookies and into server-owned storage.

Likely touch points:

- [RandomSteamGame.Client/Components/RandomGamePicker.razor](../RandomSteamGame.Client/Components/RandomGamePicker.razor)
- [RandomSteamGame/Services/SteamProvider.cs](../RandomSteamGame/Services/SteamProvider.cs)
- [RandomSteamGame/Extensions/ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs)

## Issue 3: Favorites

Goal: let users save favorite games for quick access and future filters.

Likely touch points:

- [RandomSteamGame.Client/Components/RandomGamePicker.razor](../RandomSteamGame.Client/Components/RandomGamePicker.razor)
- [RandomSteamGame/Controllers/GameController.cs](../RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Extensions/ServiceExtensions.cs](../RandomSteamGame/Extensions/ServiceExtensions.cs)

## Issue 4: Filters

Goal: add more picker filters without making the UI or API too complex.

Current random-game picker flow uses `GET /api/{provider}/random-game/details`; the older generic route is gone.

Likely touch points:

- [RandomSteamGame.Client/Components/SteamPickerForm.razor](../RandomSteamGame.Client/Components/SteamPickerForm.razor)
- [RandomSteamGame.Client/Components/RandomGamePicker.razor](../RandomSteamGame.Client/Components/RandomGamePicker.razor)
- [RandomSteamGame.Client/Services/RandomSteamApiClient.cs](../RandomSteamGame.Client/Services/RandomSteamApiClient.cs)
- [RandomSteamGame/Controllers/GameController.cs](../RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/SteamProvider.cs](../RandomSteamGame/Services/SteamProvider.cs)

## Issue 5: Picker History

Goal: track past picks so users can revisit them later.

Likely touch points:

- [RandomSteamGame/Controllers/GameController.cs](../RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/AppStatsService.cs](../RandomSteamGame/Services/AppStatsService.cs)
- [RandomSteamGame.Client/Components/RandomGamePicker.razor](../RandomSteamGame.Client/Components/RandomGamePicker.razor)

## Issue 6: CSV Export

Goal: keep CSV export available behind the right access rules if premium gating is added later.

The export already exists in the current app; this issue is about product gating and UI placement, not building the feature from scratch.

Likely touch points:

- [RandomSteamGame/Controllers/GameController.cs](../RandomSteamGame/Controllers/GameController.cs)
- [RandomSteamGame/Services/Interfaces/ISteamLibraryExportService.cs](../RandomSteamGame/Services/Interfaces/ISteamLibraryExportService.cs)
- [RandomSteamGame/Services/SteamLibraryExportService.cs](../RandomSteamGame/Services/SteamLibraryExportService.cs)

## Issue 7: User Preferences

Goal: persist picker preferences server-side instead of only in browser cookies.

Likely touch points:

- [RandomSteamGame.Client/Components/SteamPickerForm.razor](../RandomSteamGame.Client/Components/SteamPickerForm.razor)
- [RandomSteamGame.Client/Models/SteamPickerFormModel.cs](../RandomSteamGame.Client/Models/SteamPickerFormModel.cs)
- [RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs](../RandomSteamGame.Client/Services/BrowserSteamIdentityStore.cs)
- [RandomSteamGame/Services/ServerSteamIdentityReader.cs](../RandomSteamGame/Services/ServerSteamIdentityReader.cs)

## Suggested Delivery Order

1. Sign in with Steam
2. User preferences
3. Permanent blocked games
4. Favorites
5. Picker history
6. Filters
7. CSV export gating
