using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Infrastructure.Identity.PermissionManager;

public class DynamicPermissionRequirement : IAuthorizationRequirement
{
}

public class DynamicPermissionHandler : AuthorizationHandler<DynamicPermissionRequirement>
{
    private readonly IDynamicPermissionService _dynamicPermissionService;
    private readonly IHttpContextAccessor _contextAccessor;

    public DynamicPermissionHandler(
        IDynamicPermissionService dynamicPermissionService,
        IHttpContextAccessor contextAccessor
    )
    {
        _dynamicPermissionService = dynamicPermissionService;
        _contextAccessor = contextAccessor;
    }


    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DynamicPermissionRequirement requirement)
    {
        var httpContext = _contextAccessor.HttpContext;

        if (httpContext == null)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        var user = httpContext.User;

        var routeData = httpContext.GetRouteData().Values;

        var controller = routeData?["controller"]?.ToString() ?? string.Empty;
        var action = routeData?["action"]?.ToString() ?? string.Empty;
        var area = routeData?["area"]?.ToString() ?? string.Empty;
        
        if (_dynamicPermissionService.CanAccess(user, area, controller, action))
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }
}