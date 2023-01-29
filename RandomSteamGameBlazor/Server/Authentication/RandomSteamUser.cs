using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace RandomSteamGameBlazor.Server.Authentication;

public class RandomSteamUser : IdentityUser
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? VanityUrl { get; set; }
    public long? SteamId { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime RefreshTokenExpiryTime { get; set; }
}

public class PlantBuddyUserConfiguration : IEntityTypeConfiguration<RandomSteamUser>
{
    public void Configure(EntityTypeBuilder<RandomSteamUser> builder)
    {
        builder.Property(p => p.FirstName)
            .HasMaxLength(100);

        builder.Property(p => p.LastName)
            .HasMaxLength(100);
    }
}