using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Identity;

namespace Shared.Extensions;

public static class IdentityExtensions
{
    public static string FindFirstValue(this ClaimsIdentity identity, string claimType)
    {
        return identity?.FindFirst(claimType)?.Value;
    }

    public static string FindFirstValue(this IIdentity identity, string claimType)
    {
        var claimsIdentity = identity as ClaimsIdentity;
        return claimsIdentity?.FindFirstValue(claimType);
    }

    public static string GetUserId(this IIdentity identity)
    {
        return identity?.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public static T? GetUserId<T>(this IIdentity identity)
    {
        var userIdString = identity?.GetUserId();

        if (string.IsNullOrWhiteSpace(userIdString))
        {
            return default;
        }

        if (typeof(T) == typeof(Guid))
        {
            if (Guid.TryParse(userIdString, out var guidValue))
            {
                return (T)(object)guidValue;
            }

            return default;
        }

        try
        {
            return (T)Convert.ChangeType(userIdString, typeof(T), CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static string GetUserName(this IIdentity identity)
    {
        return identity?.FindFirstValue(ClaimTypes.Name);
    }

    public static string StringifyIdentityResultErrors(this IEnumerable<IdentityError> identity)
    {
        return string.Join("\n", identity.Select(c => c.Description));
    }
}