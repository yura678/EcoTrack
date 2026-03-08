using System.Security.Claims;
using Application.Common.Interfaces.Identity;
using Microsoft.AspNetCore.Http;

namespace Infrastructure.Identity.PermissionManager;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? GetCurrentEnterpriseId()
    {
        var companyIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst("CompanyId")?.Value;

        if (Guid.TryParse(companyIdClaim, out Guid companyId))
        {
            return companyId;
        }

        return null;
    }

    public Guid? GetCurrentUserId()
    {
        var userIdClaim = httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }

        return null;
    }

    public bool IsSuperAdmin()
    {
        var isSuperAdmin = httpContextAccessor.HttpContext?.User?.IsInRole("superAdmin");
        if (isSuperAdmin != null)
        {
            return isSuperAdmin.Value;
        }

        return false;
    }
}