using System.Security.Claims;

namespace RandomSteamGameBlazor.Server.Authentication;
public interface IJwtTokenGenerator
{
    string GenerateRefreshToken();
    string GenerateToken(RandomSteamUser user);
    ClaimsPrincipal GetPrincipalFromExpiredToke(string token);
}