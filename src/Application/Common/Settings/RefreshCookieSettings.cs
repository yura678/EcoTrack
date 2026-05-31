namespace Application.Common.Settings;

/// <summary>
/// Cookie attributes for the httpOnly refresh-token cookie. Defaults target the dev topology
/// (HTTPS frontend + API on localhost → schemeful same-site, so SameSite=Lax is sent). Override
/// via the "RefreshCookie" config section in prod — e.g. set a shared Domain, or SameSite=None
/// if the frontend and API are deployed cross-site. SameSite is a string so this POCO stays free
/// of ASP.NET types; it's parsed in the Api layer.
/// </summary>
public class RefreshCookieSettings
{
    public string Name { get; set; } = "ecotrack_refresh";
    public string Path { get; set; } = "/api/v1/auth";
    public string SameSite { get; set; } = "Lax";
    public bool Secure { get; set; } = true;
    public string? Domain { get; set; }

    /// <summary>Cookie lifetime in days — keep in sync with IdentitySettings.RefreshExpirationDays.</summary>
    public int ExpirationDays { get; set; } = 14;
}
