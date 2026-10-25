# Agent instructions — RandomSteamGame

These instructions apply throughout this repository. Read `README.md` and `docs/developer-handoff.md` for the fuller architecture and operational context. When documentation and code disagree, inspect the current implementation before making assumptions.

## Working agreement

- Check the `dev` branch first. If it does not exist, use the default branch. Make changes on an appropriate working branch and propose a PR unless explicitly directed otherwise; do not push directly to `main`, force-push, merge, or deploy without permission.
- Inspect the working tree before editing. Preserve unrelated or uncommitted work. Keep changes focused, understandable, and consistent with the existing .NET/C# code and UI; avoid speculative abstractions and unsolicited rewrites.
- For investigation, review, or planning requests, do not modify code or infrastructure. For implementation requests, make the change, test the relevant paths, and explain any gaps.
- Treat production deployment, routing, DNS, secrets, persistent data, and live payment configuration as separately authorized work. Do not perform those operations incidentally.

## Repository map

- `RandomSteamGame/` — ASP.NET Core host, controllers, services, SQLite persistence, middleware, configuration, and server-side Blazor components.
- `RandomSteamGame.Client/` — Blazor Interactive Auto client components and browser-facing services.
- `RandomSteamGame.Shared/` — contracts shared between host and client.
- `SteamApiClient/` — Steam API access and caching.
- `RandomSteamGame.Tests/` and `SteamApiClient.Tests/` — automated tests.
- `RandomSteamGame.IrcBot/`, `RandomSteamGame.MatrixBot/`, and `RandomSteamGame.SteamClient/` — related clients/integrations; avoid changing them for unrelated website work.

The main solution is `RandomSteamGame.slnx`. The site includes the random game picker, Steam library CSV export, and support page; do not reimplement existing functionality based on an outdated task list.

## Implementation and compatibility

- Preserve existing routes, API contracts, cookie behavior, recent-pick behavior, library export, Steam API caching, and telemetry unless the task explicitly changes them.
- Blazor uses Interactive Auto. Respect server/prerender versus WebAssembly boundaries: keep server-only dependencies and secrets out of the client, and perform browser-only or JavaScript interop after the appropriate interactive lifecycle point.
- Do not introduce a new front-end framework or broad architecture change for small features.
- Keep Steam API calls and cache behavior efficient; do not accidentally turn a UI or voting request into extra calls to Steam.
- Check both public and AltNet/DN42 deployment behavior. Cookie security and forwarded-header handling are intentionally deployment-aware; never broaden trusted proxies or weaken ingress validation casually.

## SQLite and persistent state

- The app initializes its SQLite database under `Data/` using the historical filename `kgivler_com.db`. That name is intentional. Existing data includes visitors, app statistics, and recent picks.
- **Never delete, rename, overwrite, or reset a production database.** No destructive migrations or data-clearing commands without explicit approval and a recovery plan.
- Prefer additive, idempotent schema changes that work on both a new install and an existing populated database. Verify startup/migration behavior and tests for both scenarios when modifying schema.
- Follow the existing connection, concurrency, and initialization conventions. Consider SQLite writer contention when introducing new write-heavy features; keep transactions short and bounded.
- Minimize stored personal data. Do not put raw IP addresses, Steam API keys, full Steam libraries, or unnecessary visitor information in new voting or recent-pick records.

## Public endpoints and privacy

- Assume endpoints are reachable by untrusted anonymous users. Validate and bound inputs, use appropriate rate limits and antiforgery protection for state-changing browser requests, and avoid exposing internal errors.
- Respect existing cookie and telemetry privacy behavior. If implementing feature votes, use a modest browser cookie plus a **server-keyed, domain-separated** IP-derived signal only as abuse resistance—not as a claim of one vote per person. Do not store raw IPs or use plain unsalted IP hashes.
- Never commit credentials, real API keys, connection secrets, or signing keys. Use user secrets or the configured deployment mechanism for server secrets, never client-side configuration.
- Keep telemetry best-effort: diagnostics must not break core picker or export functionality.

## Support and feature-sponsorship work

- Keep ordinary donations distinct from paid feature sponsorships. Direct prospective sponsors to `https://dev.kgivler.com/#contact` **before** any payment or commitment; don't imply a donation buys a promised feature.
- The current Ko-fi destination is `https://ko-fi.com/joyfulreaper`; confirm other payment destinations against existing configuration before changing them.
- If implementing the planned feature page, distinguish: **vote** on curated candidates, **suggest** a new idea through GitHub issues, and **inquire about sponsoring** a specific feature. Do not present proposals as implemented features or promise delivery timelines without approval.
- Favor a small server-side voting implementation using the existing SQLite database, with validation, reasonable abuse controls, and tests. Don't add accounts or payment processing merely to count anonymous votes.

## Verification

From the repository root:

```bash
dotnet restore RandomSteamGame.slnx
dotnet build RandomSteamGame.slnx --no-restore
dotnet test RandomSteamGame.slnx --no-restore
```

Run focused tests for touched behavior; add or update tests when changing routes, persistence, security boundaries, or public APIs. The Docker build also runs the solution tests with a placeholder Steam API key, so avoid tests that require real credentials or live external services. Explain any checks you could not run.

Update `README.md`, `docs/developer-handoff.md`, and any directly relevant documentation when changing user-visible features, configuration, schema, or deployment assumptions. In the handoff, summarize what changed, what was tested, what remains uncertain, and any rollout considerations.
