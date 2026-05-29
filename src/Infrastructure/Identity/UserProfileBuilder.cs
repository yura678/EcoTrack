using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries;
using Application.Models.Auth;
using Application.Models.Profile;
using Domain.Entities.User;
using Infrastructure.Identity.Manager;

namespace Infrastructure.Identity;

internal class UserProfileBuilder(
    AppUserManager userManager,
    IRoleManagerService roleManagerService,
    IUserEnterpriseMembershipQueries membershipQueries)
    : IUserProfileBuilder
{
    public async Task<MyProfileInfo> BuildAsync(
        User user, Guid? activeEnterpriseId, CancellationToken cancellationToken)
    {
        var memberships = await membershipQueries
            .GetByUserIdWithRoleAndEnterpriseAsync(user.Id, cancellationToken);
        var membershipInfo = memberships.Select(m => new MembershipInfo(
            m.EnterpriseId,
            m.Enterprise?.Name ?? string.Empty,
            m.RoleId,
            m.Role?.Name ?? string.Empty,
            m.Role?.DisplayName,
            m.JoinedAt)).ToList();

        var isSuperAdmin = await userManager.IsInRoleAsync(user, "superAdmin");

        var roles = new List<string>();
        var permissions = new List<string>();

        if (isSuperAdmin) roles.Add("superAdmin");

        if (activeEnterpriseId.HasValue)
        {
            var activeMembershipOption = await membershipQueries
                .GetActiveByUserAndEnterpriseWithRoleAsync(
                    user.Id, activeEnterpriseId.Value, cancellationToken);
            await activeMembershipOption.IfSomeAsync(async m =>
            {
                if (m.Role is not null && !string.IsNullOrEmpty(m.Role.Name))
                    roles.Add(m.Role.Name);
                if (m.RoleId != Guid.Empty)
                {
                    // SwitchEnterprise calls BuildAsync with the new active enterprise while
                    // the request still carries the OLD JWT (token rotation happens after the
                    // response). The default lookup would apply the Role/RoleClaim tenant
                    // filter against the old tenant and return zero permissions — leading to
                    // empty navigation on the first switch. Cross-tenant lookup avoids this.
                    var rolePermissions = await roleManagerService
                        .GetDynamicPermissionClaimsByRoleIdIgnoringTenantAsync(m.RoleId);
                    permissions.AddRange(rolePermissions);
                }
            });
        }

        return new MyProfileInfo(
            UserId: user.Id,
            Email: user.Email ?? string.Empty,
            EmailConfirmed: user.EmailConfirmed,
            Name: user.Name,
            FamilyName: user.FamilyName,
            ActiveEnterpriseId: activeEnterpriseId,
            IsSuperAdmin: isSuperAdmin,
            Roles: roles,
            Permissions: permissions,
            Memberships: membershipInfo);
    }
}
