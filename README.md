# Random Steam Game Picker

Random Steam Game Picker chooses a game from your own public Steam library and can export that library as CSV. Enter a 17-digit Steam ID, Steam vanity name, or Steam Community vanity URL, optionally limit the picker to unplayed games, and let it answer the important question: **What should I play?**

**Live application:** [https://randomsteam.kgivler.com](https://randomsteam.kgivler.com)

It is made for anyone with a large backlog, limited decision-making energy, and a suspicious tendency to press **Choose Again**.

## Screenshots

### Library picker

![Random Steam Game Picker home page](docs/images/RandomSteam_Main.png)

### Selected game

![Random Steam Game Picker result page](docs/images/RandomSteam_Game.png)

## Features

- Accepts a 17-digit Steam ID, vanity name, or Steam Community vanity URL
- Selects a random game the user owns from their public Steam library
- Filters the pool to games with no Steam playtime recorded when unplayed-only mode is enabled
- Supports repeated picks without returning to the home page
- Caches Steam library and Store API data to reduce upstream requests
- Blocks unwanted games from future picks in the current browser and can reset the blocked list
- Refreshes cached library data after the user's Steam library changes
- Displays the selected game's description and recorded playtime
- Opens the selected game through the Steam desktop client
- Exports a public Steam library from the [`/library-export`](https://randomsteam.kgivler.com/library-export) page
- Includes game name, App ID, playtime, last-played time, and Steam Deck compatibility in CSV exports
- Accepts numeric Steam IDs, vanity names, and Steam Community vanity URLs for both picking and exporting

Steam must be able to access the profile's game details. The saved Steam identity, unplayed-only preference, blocked games, and library-refresh timing are stored in browser cookies, so they do not synchronize across browsers or devices. The application does not require Steam sign-in.

## How It Works

1. Enter a Steam ID or vanity URL.
2. The application loads the user's public Steam library.
3. It removes games that do not match the selected options or that were blocked in the current browser.
4. It randomly selects one eligible game and loads its details.
5. Play it, choose again, or block it and roll once more.

### Library export

1. Open `/library-export` and enter a Steam ID, vanity name, or Steam Community vanity URL.
2. Vanity input is resolved to a numeric Steam ID through the Steam Web API.
3. The application loads the public library and fetches Steam Deck compatibility in batches.
4. The browser downloads `steam-library-{steamId}.csv` with these columns:

   | Column | Value |
   | --- | --- |
   | `game` | Steam game name |
   | `id` | Steam App ID |
   | `hours` | Total recorded playtime in decimal hours |
   | `last_played` | UTC timestamp in `yyyy-MM-ddTHH:mm:ssZ` format, or blank when Steam reports no meaningful date |
   | `steam_deck` | `verified`, `playable`, `unsupported`, or `unknown` |

If Deck compatibility cannot be retrieved for a game, the export uses `unknown` rather than failing the whole download. Exports are limited to one request per IP address every 72 hours to protect Steam API capacity.

## Tech Stack

- .NET 10 and ASP.NET Core
- Blazor Web App with Interactive Auto components
- Steam Web API and Steam Store API
- ASP.NET Core HybridCache with memory and SQLite-backed caching
- SQLite for lightweight application data
- Server-rendered Razor components with Interactive Auto picker and export forms
- ASP.NET Core rate limiting, Data Protection, and liveness/readiness health checks
- Optional Mission Control telemetry

## Repository Overview

| Project | Responsibility |
| --- | --- |
| `RandomSteamGame` | ASP.NET Core host, routed pages, controllers, middleware, persistence, and server services |
| `RandomSteamGame.Client` | Interactive picker components, browser state, and the browser-facing API client |
| `RandomSteamGame.Shared` | Contracts and abstractions shared by the host and client |
| `SteamApiClient` | Steam Web API and Store API clients plus caching |
| `RandomSteamGame.Tests` | Host, controller, provider, persistence, and component-supporting tests |
| `SteamApiClient.Tests` | Tests for the Steam integration library |

The central picker flow is `GameController` → `GameProviderFactory` → `SteamProvider` → `SteamApiClient`. The `/library-export` page is server-rendered for metadata and indexability, while `LibraryExportForm` uses Interactive Auto for browser identity access and vanity resolution.

## Running Locally

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [Steam Web API key](https://steamcommunity.com/dev/apikey)

### Setup

Clone the repository:

```powershell
git clone https://github.com/JoyfulReaper/RandomSteamGame.git
cd RandomSteamGame
```

Store the Steam API key with .NET user secrets. Do not put a real key in a tracked configuration file:

```powershell
dotnet user-secrets set "Steam:ApiKey" "YOUR_STEAM_WEB_API_KEY" --project RandomSteamGame/RandomSteamGame.csproj
```

Restore, build, and run the application:

```powershell
dotnet restore RandomSteamGame.slnx
dotnet build RandomSteamGame.slnx --no-restore
dotnet run --project RandomSteamGame/RandomSteamGame.csproj --no-build
```

The development profile listens at [http://localhost:5182](http://localhost:5182).

SQLite data and development Data Protection keys are created automatically. Mission Control is disabled by default and is not required for local development.

## Configuration

The only secret required for normal local use is the Steam Web API key:

| Configuration key | Environment variable | Purpose |
| --- | --- | --- |
| `Steam:ApiKey` | `Steam__ApiKey` | Reads public library data from the Steam Web API |

The tracked [`RandomSteamGame/appsettings.json`](RandomSteamGame/appsettings.json) also contains non-secret settings for caching, rate limiting, allowed origins, canonical hosts, Data Protection, and optional Mission Control telemetry.

Important operational settings include:

| Configuration key | Purpose |
| --- | --- |
| `Application:CanonicalOrigin` | Production origin used for canonical and social metadata |
| `Application:BetaHost` | Host that receives `X-Robots-Tag: noindex, nofollow` |
| `DataProtection:KeysPath` | Optional persistent Data Protection key-ring location |
| `Steam:ConnectionString` | SQLite connection string for Steam response caching |
| `Steam:Cache:*` | Cache durations for libraries, app details, vanity results, and Deck compatibility |
| `Steam:RateLimiting:*` | General Steam API request limit; CSV export has a separate one-per-IP/72-hour policy |
| `Cors:AllowedOrigins` | Browser origins permitted to call the API |
| `MissionControl:*` | Optional deployment and game-pick telemetry configuration |
| `Telemetry:VisitorHashKey` | Optional key used to pseudonymize visitor identifiers for telemetry |

When `DataProtection:KeysPath` is empty, development and non-Windows deployments use `.keys/data-protection` beneath the application content root. Production Windows deployments use a machine-level application-data directory.

## Docker

The repository Dockerfile builds on the .NET 10 SDK image, runs the full Release test suite as an image-build gate, publishes the application, and runs it as the image's non-root application user.

The Dockerfile declares port `5182`. Set `ASPNETCORE_HTTP_PORTS=5182` in the container environment, or publish whichever ASP.NET Core port your deployment configures. Mount persistent storage for the SQLite files and Data Protection key ring when deploying containers that may be replaced or recreated. To use the Dockerfile's prepared key directory, set `DataProtection__KeysPath=/data-protection` and mount a volume there. The application provides:

- `/health/live` for process liveness
- `/health/ready` for local dependency readiness, including SQLite and the Data Protection key directory

The production deployment is designed to run behind a loopback reverse proxy or Cloudflare Tunnel. Forwarded client IP and scheme headers are accepted only from loopback proxies; update the trusted-proxy configuration if a proxy reaches the application from another address, especially when relying on per-IP rate limiting.

## Search and indexing

The home page and library-export page publish canonical URLs, descriptions, and social metadata. The application also serves `robots.txt`, `sitemap.xml`, and `WebApplication` JSON-LD. The configured beta host sends `X-Robots-Tag: noindex, nofollow`, and generated random-game result pages are excluded from indexing.

## Tests

Run all solution tests from the repository root:

```powershell
dotnet test RandomSteamGame.slnx
```

To run an individual test project:

```powershell
dotnet test RandomSteamGame.Tests/RandomSteamGame.Tests.csproj
dotnet test SteamApiClient.Tests/SteamApiClient.Tests.csproj
```

## Contributing

Focused issues and pull requests are welcome. Describe the user-visible behavior, keep unrelated changes separate, and add or update tests when the behavior is testable.

Use [GitHub Issues](https://github.com/JoyfulReaper/RandomSteamGame/issues) for bug reports and feature proposals.

## Roadmap

Planned or considered improvements include:

- Steam sign-in and account-linked preferences
- Permanent blocked games that follow the user across devices
- Favorites and picker history
- More game filters
- Saved and shareable game lists

These are future ideas, not currently implemented features.

## Additional Documentation

- [Developer handoff](docs/developer-handoff.md) — deeper architecture, request flow, caching, deployment, and debugging notes
- [Premium feature issues](docs/premium-feature-issues.md) — account-related roadmap and likely implementation areas

## License

Random Steam Game Picker is licensed under the [MIT License](LICENSE.md).

The license does not protect you from backlog guilt or from the picker selecting exactly the game you were secretly avoiding.
