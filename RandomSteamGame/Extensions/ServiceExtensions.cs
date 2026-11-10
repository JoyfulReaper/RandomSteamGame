/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using JoyfulReaperLib.MissionControl;
using JoyfulReaperLib.Sqlite;
using JoyfulReaperLib.WebStats.Sqlite;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Common.Errors;
using RandomSteamGame.Events;
using RandomSteamGame.Options;
using RandomSteamGame.Services;
using RandomSteamGame.Services.Analytics;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Interfaces;
using RandomSteamGame.Shared.Services;
using SteamApiClient;
using SteamApiClient.Settings;
using System.Security.Claims;
using System.Threading.RateLimiting;

namespace RandomSteamGame.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        const string schemaSql = """
            CREATE TABLE IF NOT EXISTS Visitors (
                IpAddress TEXT PRIMARY KEY,
                Hits INTEGER NOT NULL DEFAULT 1,
                LastSeen TEXT
            );

            CREATE TABLE IF NOT EXISTS AppStats (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                RandomGamesGenerated INTEGER NOT NULL DEFAULT 0,
                LibrariesExported INTEGER NOT NULL DEFAULT 0
            );

            INSERT INTO AppStats (Id, RandomGamesGenerated)
            SELECT 1, 0
            WHERE NOT EXISTS (SELECT 1 FROM AppStats WHERE Id = 1);
            """;

        var connectionString = SqliteDatabaseInitializer.Initialize("kgivler_com.db",
            schemaSql + SqliteRecentPickService.SchemaSql);
        SqliteFeatureVotingService.InitializeSchema(connectionString);
        EnsureAppStatsSchema(connectionString);

        services.AddOptions<LibraryExportOptions>()
            .Bind(config.GetSection(LibraryExportOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => Enum.IsDefined(options.RateLimitMode), "Steam:LibraryExport:RateLimitMode must be PerIp or Global.")
            .ValidateOnStart();

        services.AddOptions<ApplicationOptions>()
            .Bind(config.GetSection(ApplicationOptions.SectionName))
            .Validate(options => Enum.IsDefined(options.NetworkMode), "Application:NetworkMode must be Public or AltNet.")
            .Validate(options => CanonicalUrlService.TryGetOrigin(options, out _), CanonicalUrlService.OriginValidationMessage)
            .ValidateOnStart();
        services.AddOptions<IngressOptions>()
            .Bind(config.GetSection(IngressOptions.SectionName))
            .Validate(options => options.TrustedProxies is null || options.TrustedProxies.All(proxy => System.Net.IPAddress.TryParse(proxy, out _)),
                "Ingress:TrustedProxies must contain only IP addresses.")
            .ValidateOnStart();



        services.AddOptions<RandomSteamAnalyticsOptions>()
            .Bind(config.GetSection(RandomSteamAnalyticsOptions.SectionName))
            .Validate(options =>
            {
                if (!options.Enabled)
                    return true;

                if (options.ApiKey is not { Length: >= 32 and <= 256 })
                    return false;

                if (!Uri.TryCreate(options.ArchiveBaseUrl, UriKind.Absolute, out var uri))
                    return false;

                return (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                    && !string.IsNullOrEmpty(uri.Host)
                    && uri.AbsolutePath == "/"
                    && string.IsNullOrEmpty(uri.Query)
                    && string.IsNullOrEmpty(uri.Fragment)
                    && string.IsNullOrEmpty(uri.UserInfo);
            }, "RandomSteamAnalytics requires a valid Archive origin and 32-256 character API key when enabled.")
            .ValidateOnStart();


        services.AddHttpClient<RandomSteamAnalyticsClient>((provider, client) =>
        {
            var options = provider
                .GetRequiredService<IOptions<RandomSteamAnalyticsOptions>>()
                .Value;

            client.Timeout = TimeSpan.FromSeconds(5);

            if (options.Enabled)
                client.BaseAddress = new Uri(options.ArchiveBaseUrl!.TrimEnd('/') + "/");
        });


        services.Configure<HostingOptions>(config.GetSection(HostingOptions.SectionName));
        services.Configure<TelemetryOptions>(config.GetSection(TelemetryOptions.SectionName));
        services.AddSingleton<IVisitorIdProvider, VisitorIdProvider>();

        services.AddJoyfulReaperSqliteHitCounter(options =>
        {
            options.ConnectionString = connectionString;
        });

        services.AddApplicationDataProtection(config, env);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = ".RandomSteamGame.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";

                options.LoginPath = "/auth/login";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;

                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments(
                        "/api/admin/analytics",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        context.Response.Headers.CacheControl = "private, no-store";
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };

            })
            .AddSteam(options =>
            {
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            });



        services.AddAuthorization();

        services.AddOptions<AuthorizationOptions>()
            .Configure<IConfiguration>((options, configuration) =>
            {
                var adminSteamIds = configuration
                    .GetSection("Authentication:AdminSteamIds")
                    .Get<string[]>() ?? [];

                if (adminSteamIds.Any(id => id is null || id.Length != 17 || !id.All(char.IsAsciiDigit)))
                {
                    throw new InvalidOperationException(
                        "Authentication:AdminSteamIds must contain only 17-digit SteamID64 values.");
                }

                var adminIdentities = adminSteamIds
                    .Select(id => $"https://steamcommunity.com/openid/id/{id}")
                    .ToHashSet(StringComparer.Ordinal);

                options.AddPolicy("SteamAdmin", policy =>
                {
                    policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme);
                    policy.RequireAuthenticatedUser();
                    policy.RequireAssertion(context =>
                    {
                        var identifier = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                        return identifier is not null && adminIdentities.Contains(identifier);
                    });
                });
            })
            .ValidateOnStart();



        services.AddBlazorServices();
        services.AddApiServices();
        services.AddSteamIdentityServices();
        services.AddGameProviderServices();
        services.AddPersistenceServices(connectionString);
        services.AddSteamServices(config);
        services.AddApplicationCors(config, env);

        services.AddSteamRateLimiting();
        services.AddFeatureVotingRateLimits();

        services.AddApplicationHealthChecks();
        services.AddMemoryCache();
        services.AddScoped<GameApplicationService>();
        services.AddScoped<IRandomSteamApiClient, ServerRandomSteamApiClient>();
        services.AddSingleton<IBetaAvailabilityService, BetaAvailabilityService>();
        services.AddSingleton<CanonicalUrlService>();
        services.AddSingleton<DeploymentCookiePolicy>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<GlobalLibraryExportCooldownTracker>();

        services.AddMissionControlClient(
            config.GetSection(MissionControlClientOptions.SectionName));
        services.AddHostedService<ApplicationStartupTelemetryService>();

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = ".RandomSteamGame.Antiforgery.v2";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.Path = "/";
        });
        services.AddOptions<AntiforgeryOptions>().Configure<DeploymentCookiePolicy>(
            (options, policy) => options.Cookie.SecurePolicy = policy.GetAntiforgerySecurePolicy(env));

        return services;
    }

    internal static string UseDefaultSqliteCache(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            // File-backed databases do not need shared cache for separate connections.
            // Its table locks report SQLITE_LOCKED and bypass the normal busy timeout.
            Cache = SqliteCacheMode.Default
        };

        return builder.ToString();
    }

    private static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck(
                "process",
                () => HealthCheckResult.Healthy(),
                tags: ["live"])
            .AddCheck<LocalReadinessHealthCheck>(
                "local-dependencies",
                tags: ["ready"]);

        return services;
    }

    private static IServiceCollection AddApplicationDataProtection(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        var dataProtectionSection = config.GetSection("DataProtection");
        var applicationName = dataProtectionSection["ApplicationName"];
        if (string.IsNullOrWhiteSpace(applicationName))
        {
            applicationName = "RandomSteamGame";
        }

        var configuredKeysPath = dataProtectionSection["KeysPath"];
        var keysPath = string.IsNullOrWhiteSpace(configuredKeysPath)
            ? GetDefaultDataProtectionKeysPath(env)
            : configuredKeysPath;

        Directory.CreateDirectory(keysPath);

        var dataProtectionBuilder = services.AddDataProtection()
            .SetApplicationName(applicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        if (OperatingSystem.IsWindows())
        {
            dataProtectionBuilder.ProtectKeysWithDpapi(protectToLocalMachine: true);
        }

        services.AddSingleton(new DataProtectionSettings(applicationName, keysPath));

        return services;
    }

    private static IServiceCollection AddBlazorServices(this IServiceCollection services)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();

        return services;
    }

    private static string GetDefaultDataProtectionKeysPath(IWebHostEnvironment env)
    {
        if (env.IsDevelopment() || !OperatingSystem.IsWindows())
        {
            return Path.Combine(env.ContentRootPath, ".keys", "data-protection");
        }

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var keysPath = Path.Combine(programData, "JoyfulReaper", "RandomSteamGame", "DataProtectionKeys");

        // Keep this directory outside the deploy folder so keys survive app pool recycles,
        // deployments, and restarts. The IIS identity needs read/write/create access, and
        // deleting the key ring invalidates existing antiforgery/auth cookies.
        return keysPath;
    }

    private static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers();
        services.AddHttpContextAccessor();
        services.AddProblemDetails();
        services.AddTransient<ProblemDetailsFactory, RandomSteamProblemDetailsFactory>();

        return services;
    }

    private static IServiceCollection AddSteamIdentityServices(this IServiceCollection services)
    {
        services.AddScoped<IBrowserSteamIdentityStore, BrowserSteamIdentityStore>();
        services.AddScoped<ISteamIdentityWriter, ServerSteamIdentityWriter>();
        services.AddScoped<ISteamIdentityReader, ServerSteamIdentityReader>();

        return services;
    }

    private static IServiceCollection AddGameProviderServices(
        this IServiceCollection services)
    {
        var providerType = typeof(IGameProvider);

        var implementations = typeof(SteamProvider)
            .Assembly
            .GetTypes()
            .Where(type =>
                providerType.IsAssignableFrom(type) &&
                !type.IsInterface &&
                !type.IsAbstract);

        foreach (var implementation in implementations)
        {
            services.AddScoped(providerType, implementation);
        }

        services.AddScoped<GameProviderFactory>();
        services.AddScoped<IOwnedGamesCacheResetTracker, OwnedGamesCacheResetTracker>();
        services.AddSingleton<OwnedGamesRefreshAdmissionCoordinator>();

        return services;
    }

    private static IServiceCollection AddPersistenceServices(this IServiceCollection services, string connectionString)
    {
        var recentPickConnectionString = UseDefaultSqliteCache(connectionString);

        services.AddScoped<IHtmlSanitizerService, HtmlSanitizerService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IAppStatsService, AppStatsService>();
        services.AddScoped<IRecentPickService>(_ => new SqliteRecentPickService(recentPickConnectionString));
        services.AddScoped<IFeatureVotingService>(_ => new SqliteFeatureVotingService(recentPickConnectionString));
        services.AddScoped<ILibraryExportCooldownTracker, LibraryExportCooldownTracker>();
        services.AddScoped<ISteamLibraryExportService, SteamLibraryExportService>();
        services.AddScoped<SqliteConnection>(_ => new SqliteConnection(connectionString));

        return services;
    }

    private static IServiceCollection AddSteamServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSteamApiClient(config);
        return services;
    }

    private static IServiceCollection AddApplicationCors(
        this IServiceCollection services,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        services.AddCors(options =>
        {
            var configuredOrigins = config
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            var allowedOrigins = new List<string>(configuredOrigins);

            if (env.IsDevelopment())
            {
                allowedOrigins.Add("http://localhost:5500");
                allowedOrigins.Add("http://127.0.0.1:5500");
                allowedOrigins.Add("http://localhost:3000");
            }

            if (allowedOrigins.Count == 0)
            {
                throw new InvalidOperationException("No CORS origins configured.");
            }

            options.AddPolicy("DefaultCors", policy =>
            {
                policy.WithOrigins(allowedOrigins.ToArray())
                    .WithMethods("GET", "POST")
                    .WithHeaders("Content-Type", "Authorization");
            });
        });

        return services;
    }

    private static IServiceCollection AddSteamRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter();
        services.AddSingleton(provider => new SteamApiRequestLimiter(
            provider.GetRequiredService<IOptions<SteamClientApiOptions>>().Value.RateLimiting));

        services
            .AddOptions<Microsoft.AspNetCore.RateLimiting.RateLimiterOptions>()
            .Configure<Microsoft.Extensions.Options.IOptions<LibraryExportOptions>, SteamApiRequestLimiter>(
                (options, libraryExportOptions, apiLimiter) =>
                {
                    options.GlobalLimiter =
                        PartitionedRateLimiter.Create<HttpContext, string>(
                            httpContext =>
                            {
                                var policyName = httpContext
                                    .GetEndpoint()?
                                    .Metadata
                                    .GetMetadata<EnableRateLimitingAttribute>()?
                                    .PolicyName;

                                if (!string.Equals(
                                        policyName,
                                        "library_export_limiter",
                                        StringComparison.Ordinal))
                                {
                                    return RateLimitPartition.GetNoLimiter(
                                        "non-library-export");
                                }

                                return RateLimitPartition.GetConcurrencyLimiter(
                                    partitionKey: "library-export-global",
                                    factory: _ => new ConcurrencyLimiterOptions
                                    {
                                        PermitLimit =
                                            libraryExportOptions.Value.EffectiveGlobalConcurrency,
                                        QueueLimit = 0,
                                        QueueProcessingOrder =
                                            QueueProcessingOrder.OldestFirst
                                    });
                            });

                    options.AddPolicy("steam_api_limiter", _ =>
                        RateLimitPartition.Get("steam-api", _ => apiLimiter));

                    options.AddPolicy(
                        "library_export_limiter",
                        httpContext =>
                        {
                            if (libraryExportOptions.Value.RateLimitMode == LibraryExportRateLimitMode.Global)
                            {
                                return RateLimitPartition.GetNoLimiter("library-export-global-policy");
                            }

                            var partitionKey =
                                LibraryExportRateLimitPartitionKey.From(
                                    httpContext.Connection.RemoteIpAddress);

                            return RateLimitPartition.GetConcurrencyLimiter(
                                partitionKey: partitionKey,
                                factory: _ => new ConcurrencyLimiterOptions
                                {
                                    PermitLimit = 1,
                                    QueueLimit = 0,
                                    QueueProcessingOrder =
                                        QueueProcessingOrder.OldestFirst
                                });
                        });

                    options.OnRejected = async (context, token) =>
                    {
                        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";

                        var policyName = context.HttpContext
                            .GetEndpoint()?
                            .Metadata
                            .GetMetadata<EnableRateLimitingAttribute>()?
                            .PolicyName;

                        if (string.Equals(
                                policyName,
                                "library_export_limiter",
                                StringComparison.Ordinal))
                        {
                            var httpContext = context.HttpContext;
                            var provider = httpContext.Request.RouteValues["provider"]?.ToString();
                            var remoteIp = httpContext.Connection.RemoteIpAddress;
                            var visitorIdProvider = httpContext.RequestServices.GetRequiredService<IVisitorIdProvider>();

                            var visitorId = remoteIp is null
                                ? null
                                : visitorIdProvider.GetVisitorId(
                                    remoteIp.ToString());

                            var applicationOptions = httpContext.RequestServices
                                .GetRequiredService<IOptions<ApplicationOptions>>()
                                .Value;

                            var missionControlClient = httpContext.RequestServices
                                .GetRequiredService<IMissionControlClient>();

                            long? retryAfterSeconds = null;
                            if (libraryExportOptions.Value.RateLimitMode == LibraryExportRateLimitMode.Global)
                            {
                                var remaining = httpContext.RequestServices.GetRequiredService<GlobalLibraryExportCooldownTracker>().GetRetryAfter();
                                retryAfterSeconds = remaining is null ? 5 : Math.Max(1, (long)Math.Ceiling(remaining.Value.TotalSeconds));
                                httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                httpContext.Response.Headers.CacheControl = "private, no-store";
                                httpContext.Response.Headers["CDN-Cache-Control"] = "no-store";
                            }

                            _ = missionControlClient.TryPublishAsync(
                                eventType: RandomSteamGameEventTypes.LibraryExportRejected,
                                payload: new LibraryExportRejectedEvent(
                                    VisitorId: visitorId,
                                    Provider: provider,
                                    IngressNetwork: RandomSteamIngressNetwork.FromHost(httpContext.Request.Host.Host),
                                    Reason: LibraryExportRejectionReason.Capacity,
                                    RetryAfterSeconds: retryAfterSeconds,
                                    CommitSha: string.IsNullOrWhiteSpace(
                                        applicationOptions.CommitSha)
                                        ? null
                                        : applicationOptions.CommitSha),
                                payloadTypeInfo: RandomSteamGameJsonContext.Default.LibraryExportRejectedEvent,
                                occurredAt: DateTimeOffset.UtcNow,
                                correlationId: Guid.NewGuid().ToString("N"),
                                cancellationToken: CancellationToken.None);

                            await httpContext.Response.WriteAsync(
                                "Steam library CSV export capacity is currently busy. " +
                                "Please wait for an in-progress export to finish and try again.",
                                token);

                            return;
                        }

                        await context.HttpContext.Response.WriteAsync(
                            "Too many requests. Please slow down and try again in a few seconds.",
                            token);
                    };
                });

        return services;
    }

    private static void EnsureAppStatsSchema(
        string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var hasLibrariesExported = false;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(AppStats);";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var columnName = reader.GetString(1);

                if (string.Equals(columnName, "LibrariesExported", StringComparison.OrdinalIgnoreCase))
                {
                    hasLibrariesExported = true;
                    break;
                }
            }
        }

        if (hasLibrariesExported)
        {
            return;
        }

        using var alterCommand = connection.CreateCommand();

        alterCommand.CommandText = """
            ALTER TABLE AppStats
            ADD COLUMN LibrariesExported
            INTEGER NOT NULL DEFAULT 0;
            """;

        alterCommand.ExecuteNonQuery();
    }

}

internal sealed record DataProtectionSettings(string ApplicationName, string KeysPath);
