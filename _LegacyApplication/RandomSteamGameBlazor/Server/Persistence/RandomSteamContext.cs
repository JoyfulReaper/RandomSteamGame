using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RandomSteamGameBlazor.Server.Persistence.Entities;

namespace RandomSteamGameBlazor.Server.Persistence;

public class RandomSteamContext : IdentityDbContext
{
    public RandomSteamContext(DbContextOptions<RandomSteamContext> options)
        : base(options) { }

    public DbSet<FavoriteGame> FavoriteGames => Set<FavoriteGame>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(RandomSteamContext).Assembly);

        base.OnModelCreating(builder);
    }
}
