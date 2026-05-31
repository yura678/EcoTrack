namespace Application.Models.Auth;

/// <summary>
/// Carries the antiforgery request token in the response body of GET /auth/csrf. The SPA holds
/// it in memory and echoes it back in the X-XSRF-TOKEN header. This is the cross-origin-safe
/// channel: when the SPA and API live on different domains the JS-readable XSRF-TOKEN cookie is
/// not visible to the SPA, so the body is the only reliable way to hand the token to it.
/// </summary>
public record CsrfTokenResponse(string CsrfToken);
