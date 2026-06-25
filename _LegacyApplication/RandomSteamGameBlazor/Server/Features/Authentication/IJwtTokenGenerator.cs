using ErrorOr;
using System.Security.Claims;

namespace RandomSteamGameBlazor.Server.Features.Authentication;
public interface IJwtTokenGenerator
{
    string GenerateRefreshToken();
    string GenerateToken(RandomSteamUser user);
    ErrorOr<ClaimsPrincipal> GetPrincipalFromExpiredToken(string token);
}