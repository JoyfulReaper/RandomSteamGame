using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace RandomSteamGame.Client.Interop;

[SupportedOSPlatform("browser")]
public static partial class CookieInterop
{
    [JSImport("getCookie", "CookieModule")]
    public static partial string GetCookie(string name);

    [JSImport("setCookie", "CookieModule")]
    public static partial void SetCookie(string name, string value, int days);
}