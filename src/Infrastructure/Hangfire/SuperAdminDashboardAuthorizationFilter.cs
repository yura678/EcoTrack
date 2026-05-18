using Hangfire.Dashboard;

namespace Infrastructure.Hangfire;

/// <summary>
/// Restricts /hangfire dashboard to users authenticated with the superAdmin role.
/// Falls back to deny when no HttpContext.User claims are present.
/// </summary>
public class SuperAdminDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole("superAdmin");
    }
}
