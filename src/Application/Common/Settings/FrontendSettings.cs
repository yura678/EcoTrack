namespace Application.Common.Settings;

/// <summary>
/// Connection points the backend uses when it has to put a clickable link into an outbound
/// channel (password-reset email, invitation email, etc.). Lives in config because the
/// frontend host varies between local dev (<c>http://localhost:5173</c>), staging, and prod.
/// </summary>
public class FrontendSettings
{
    /// <summary>
    /// Origin (scheme + host + optional port) of the SPA. No trailing slash. Paths the backend
    /// appends are absolute and start with <c>/</c>. Empty string would produce broken email
    /// links — validated at startup is intentionally avoided so dev/test runs without a value
    /// don't refuse to boot.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
}
