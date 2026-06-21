/*
 * Random Steam Game
 * 
 * Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RandomSteamGameBlazor.Server.Common.Services;
using RandomSteamGameBlazor.Server.Features.Authentication;
using RandomSteamGameBlazor.Server.Persistence;
using RandomSteamGameBlazor.Server.Services;
using System.Reflection;
using System.Text;

namespace RandomSteamGameBlazor.Server;

public static class ServerDependencyInjection
{
    public static IServiceCollection AddRandomSteamGame(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ISteamService, SteamService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddMapster();
        services.AddAuthentication(configuration);
        services.AddIdentity(configuration);

        return services;
    }

    public static IServiceCollection AddMapster(this IServiceCollection services)
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(Assembly.GetExecutingAssembly());
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();
        return services;
    }

    public static IServiceCollection AddAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorization();
        var jwtSettings = new JwtSettings();
        configuration.Bind(JwtSettings.SectionName, jwtSettings);
        services.AddSingleton(Options.Create(jwtSettings));
        services.AddTransient<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSettings.SecretKey))
            };
        });
        return services;
    }

    public static IServiceCollection AddIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        var databaseProvider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

        services.AddDbContext<RandomSteamContext>(opts =>
        {
            if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                // SQLite connection string
                opts.UseSqlite(configuration.GetConnectionString("SqliteConnection"));
            }
            else
            {
                opts.UseSqlServer(configuration.GetConnectionString("SqlServerConnection"));
            }
        });

        services.AddIdentity<RandomSteamUser, IdentityRole>(opts =>
        {
            opts.SignIn.RequireConfirmedAccount = false;
        })
        .AddEntityFrameworkStores<RandomSteamContext>()
        .AddDefaultTokenProviders();

        return services;
    }
}