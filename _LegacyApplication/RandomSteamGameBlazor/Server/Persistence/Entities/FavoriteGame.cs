using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RandomSteamGameBlazor.Server.Persistence.Entities;

public class FavoriteGame
{
    public int Id { get; set; }
    public string RandomSteamUserId { get; set; } = default!;
    public int SteamAppId { get; set; }
}

public class FavoriteGameConfig : IEntityTypeConfiguration<FavoriteGame>
{
    public void Configure(EntityTypeBuilder<FavoriteGame> builder)
    {
        builder.Property(x => x.RandomSteamUserId)
            .IsRequired()
            .HasMaxLength(450);
    }
}