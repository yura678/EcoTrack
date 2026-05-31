using Application.Common.Settings;

namespace Api.Modules;

/// <summary>
/// Helpers for writing/clearing the httpOnly refresh-token cookie from controllers, driven by
/// <see cref="RefreshCookieSettings"/>. Kept in the Api layer because cookie/SameSite types are
/// ASP.NET-specific.
/// </summary>
public static class RefreshCookieExtensions
{
    public static void AppendRefreshCookie(this HttpResponse response, string token, RefreshCookieSettings s) =>
        response.Cookies.Append(s.Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = s.Secure,
            SameSite = ParseSameSite(s.SameSite),
            Path = s.Path,
            Domain = string.IsNullOrWhiteSpace(s.Domain) ? null : s.Domain,
            MaxAge = TimeSpan.FromDays(s.ExpirationDays),
            IsEssential = true,
        });

    public static void ClearRefreshCookie(this HttpResponse response, RefreshCookieSettings s) =>
        // Path/Domain/SameSite/Secure must match the original for the browser to drop the cookie.
        response.Cookies.Delete(s.Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = s.Secure,
            SameSite = ParseSameSite(s.SameSite),
            Path = s.Path,
            Domain = string.IsNullOrWhiteSpace(s.Domain) ? null : s.Domain,
        });

    public static SameSiteMode ParseSameSite(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "none" => SameSiteMode.None,
        "strict" => SameSiteMode.Strict,
        _ => SameSiteMode.Lax,
    };
}
