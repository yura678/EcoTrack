using Application.Models.Jwt;
using Application.Models.Profile;

namespace Application.Models.Auth;

/// <summary>
/// Bundle returned by token-issuing endpoints (login/refresh/switch-enterprise). The SPA
/// cannot decode the JWE-encrypted access token, so the profile is shipped alongside the
/// token to avoid a follow-up roundtrip to /profile/me.
/// </summary>
public record AuthSession(AccessToken Token, MyProfileInfo Profile);
