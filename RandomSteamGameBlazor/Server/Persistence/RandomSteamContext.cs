using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace RandomSteamGameBlazor.Server.Persistence;

public class RandomSteamContext : IdentityDbContext
{
    public RandomSteamContext(DbContextOptions<RandomSteamContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(RandomSteamContext).Assembly);

        base.OnModelCreating(builder);
    }
}
