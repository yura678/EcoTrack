using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Hangfire;

/// <summary>
/// Restricts /hangfire dashboard to users authenticated with the superAdmin role.
/// Falls back to deny when no HttpContext.User claims are present.
///
/// Development bypass: the API uses pure JWT bearer auth, so a browser navigating to
/// /hangfire never carries credentials — it would always 401. To keep dev iteration painless
/// we let everyone in when the host is in Development. Production / Staging still enforce
/// the role check.
/// </summary>
public class SuperAdminDashboardAuthorizationFilter(IWebHostEnvironment environment)
    : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        if (environment.IsDevelopment()) return true;

        var httpContext = context.GetHttpContext();
        var user = httpContext.User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole("superAdmin");
    }
}
