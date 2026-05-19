using Infrastructure.Identity.PermissionManager;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// Live channel that pushes newly opened ComplianceEvent records to connected browsers.
/// Connection-time permission gate mirrors the REST list endpoint: a user that cannot
/// GET /compliance-events is connected but never joined to any group, so the broadcast
/// silently passes them by.
/// </summary>
[Authorize]
public class ComplianceEventsHub(IDynamicPermissionService permissions) : Hub
{
    public const string EventOpenedMethod = "ComplianceEventOpened";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var enterpriseId = user?.FindFirst("CompanyId")?.Value;
        if (string.IsNullOrEmpty(enterpriseId))
        {
            await base.OnConnectedAsync();
            return;
        }

        // Mirror the REST policy for /compliance-events list. CanAccess key is
        // "{area}:{controller}:" so "GetPaged" is informational here; service only checks
        // area+controller. Keeping the same arguments documents the parity intent.
        if (!permissions.CanAccess(user!, area: "", controller: "ComplianceEvent", action: "GetPaged"))
        {
            await base.OnConnectedAsync();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(Guid.Parse(enterpriseId)));
        await base.OnConnectedAsync();
    }

    public static string GroupName(Guid enterpriseId) => $"enterprise:{enterpriseId}";
}
