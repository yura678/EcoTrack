using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Application.Common.Interfaces.Identity;
using Application.Features.Role.Exceptions;
using Application.Models.Identity;
using Domain.Entities.Enterprises;
using Domain.Entities.User;
using Infrastructure.Identity.Manager;
using Infrastructure.Persistence;
using LanguageExt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity.PermissionManager;

internal class RoleManagerService : IRoleManagerService
{
    private readonly AppRoleManager _roleManger;
    private readonly IActionDescriptorCollectionProvider _actionDescriptor;
    private readonly EndpointDataSource _endpointDataSource;
    private readonly AppUserManager _userManager;
    private readonly ILogger<RoleManagerService> _logger;
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public RoleManagerService(
        AppRoleManager roleManger,
        IActionDescriptorCollectionProvider actionDescriptor,
        AppUserManager userManager,
        ILogger<RoleManagerService> logger,
        ApplicationDbContext db,
        EndpointDataSource endpointDataSource,
        ICurrentUserService currentUserService)
    {
        _roleManger = roleManger;
        _actionDescriptor = actionDescriptor;
        _userManager = userManager;
        _logger = logger;
        _db = db;
        _endpointDataSource = endpointDataSource;
        _currentUserService = currentUserService;
    }

    public async Task<List<GetRolesDto>> GetRolesAsync()
    {
        var result = await _roleManger.Roles
            .Select(r => new GetRolesDto { Id = r.Id.ToString(), Name = r.Name!, EnterpriseId = r.EnterpriseId })
            .ToListAsync();

        return result;
    }

    public async Task<List<GetRolesDto>> GetEnterpriseRolesAsync(Guid enterpriseId)
    {
        var result = await _roleManger.Roles
            .Where(c => c.EnterpriseId == enterpriseId)
            .Select(r => new GetRolesDto { Id = r.Id.ToString(), Name = r.Name!, EnterpriseId = r.EnterpriseId })
            .ToListAsync();

        return result;
    }

    public async Task<(IdentityResult Result, Guid? CreatedRoleId)> CreateRoleAsync(CreateRoleDto model)
    {
        var role = new Role
        {
            Name = model.RoleName,
            DisplayName = model.DisplayName,
            EnterpriseId = model.EnterpriseId
        };

        var result = await _roleManger.CreateAsync(role);

        return (result, result.Succeeded ? role.Id : null);
    }

    public async Task SeedAllDynamicPermissionsAsync(Guid roleId)
    {
        var role = await _roleManger.Roles
            .Include(x => x.Claims)
            .SingleOrDefaultAsync(x => x.Id == roleId);

        if (role == null)
        {
            // Almost always means the caller forgot to commit the new role before seeding —
            // Phase A's AutoSaveChanges=false on the role store leaves a freshly-created role
            // only in the change tracker until the caller's SaveChanges runs. Throwing instead
            // of silently returning surfaces the mistake during dev rather than producing an
            // admin with zero permission claims at runtime.
            throw new InvalidOperationException(
                $"Role {roleId} not found when seeding dynamic permissions. " +
                "Did the caller forget to call SaveChangesAsync after CreateRoleAsync?");
        }

        var allActions = await GetPermissionActionsAsync();
        var existingKeys = role.Claims
            .Where(c => c.ClaimType == ConstantPolicies.DynamicPermission)
            .Select(c => c.ClaimValue)
            .ToHashSet();

        foreach (var action in allActions)
        {
            if (existingKeys.Contains(action.Key))
                continue;

            role.Claims.Add(new RoleClaim
            {
                ClaimType = ConstantPolicies.DynamicPermission,
                ClaimValue = action.Key,
                CreatedClaim = DateTime.UtcNow,
                RoleId = role.Id
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<Either<RoleException, bool>> DeleteRoleAsync(Guid roleId)
    {
        var role = await _roleManger.Roles.Include(r => r.Claims)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null || string.IsNullOrEmpty(role.Name))
            return new RoleNotFoundException(Guid.Empty, roleId);

        // FK constraints on user_enterprise_membership.role_id and enterprise_invitations.role_id
        // are configured OnDelete=Restrict, so any reference crashes SaveChanges with PG 23503.
        // Pre-check explicitly and surface a 409 with counts so the operator can see what blocks
        // the delete. IgnoreQueryFilters because a role may be referenced from a tenant outside
        // the current scope (SuperAdmin acting on a tenant role).
        var activeMemberships = await _db.Set<UserEnterpriseMembership>()
            .IgnoreQueryFilters()
            .CountAsync(m => m.RoleId == roleId && m.RevokedAt == null);
        var historicalMemberships = await _db.Set<UserEnterpriseMembership>()
            .IgnoreQueryFilters()
            .CountAsync(m => m.RoleId == roleId && m.RevokedAt != null);
        var pendingInvitations = await _db.Set<EnterpriseInvitation>()
            .IgnoreQueryFilters()
            .CountAsync(i => i.RoleId == roleId && !i.IsUsed && i.ExpiresAt > DateTime.UtcNow);

        if (activeMemberships > 0 || historicalMemberships > 0 || pendingInvitations > 0)
        {
            return new RoleInUseException(
                roleId, activeMemberships, historicalMemberships, pendingInvitations);
        }

        // Defensive: even though the pre-check ruled out active memberships, bump SecurityStamp
        // for any race-window inserts. The list is normally empty.
        var affectedUserIds = await _db.Set<UserEnterpriseMembership>()
            .Where(m => m.RoleId == roleId && m.RevokedAt == null)
            .Select(m => m.UserId)
            .ToListAsync();

        foreach (var userId in affectedUserIds)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is not null)
                await _userManager.UpdateSecurityStampAsync(user);
        }

        _db.RemoveRange(role.Claims);
        _db.Remove(role);
        await _db.SaveChangesAsync();

        return true;
    }

    public Task<List<ActionDescriptionDto>> GetPermissionActionsAsync()
    {
        var actions = new List<ActionDescriptionDto>();

        var actionDescriptors = _actionDescriptor.ActionDescriptors.Items;
        string controllerName = "";
        foreach (var actionDescriptor in actionDescriptors)
        {
            try
            {
                var descriptor = (ControllerActionDescriptor)actionDescriptor;

                var hasPermission = descriptor.ControllerTypeInfo.GetCustomAttribute<AuthorizeAttribute>()?
                    .Policy == ConstantPolicies.DynamicPermission; //||
                // descriptor.MethodInfo.GetCustomAttribute<AuthorizeAttribute>()?
                //   .Policy == ConstantPolicies.DynamicPermission;


                if (hasPermission && (controllerName != descriptor.ControllerName))
                {
                    actions.Add(new ActionDescriptionDto
                    {
                        // ActionName = descriptor.ActionName,
                        ControllerName = descriptor.ControllerName,
                        ActionDisplayName = descriptor.MethodInfo.GetCustomAttribute<DisplayAttribute>()?.Name,
                        AreaName = descriptor.ControllerTypeInfo.GetCustomAttribute<AreaAttribute>()?.RouteValue,
                        ControllerDisplayName =
                            descriptor.ControllerTypeInfo.GetCustomAttribute<DisplayAttribute>()?.Name,
                        ControllerDescription = descriptor.ControllerTypeInfo.GetCustomAttribute<DisplayAttribute>()
                            ?.Description
                    });
                    controllerName = descriptor.ControllerName;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
            }
        }

        return Task.FromResult(actions);
    }

    public async Task<Option<RolePermissionDto>> GetRolePermissionsAsync(Guid roleId)
    {
        var role = await _roleManger.Roles
            .Include(x => x.Claims)
            .SingleOrDefaultAsync(x => x.Id == roleId);

        if (role == null)
            return Option<RolePermissionDto>.None;

        var dynamicActions = await this.GetPermissionActionsAsync();
        return new RolePermissionDto
        {
            Role = role,
            Actions = dynamicActions
        };
    }

    public async Task<bool> ChangeRolePermissionsAsync(EditRolePermissionsDto model)
    {
        var role = await _roleManger.Roles
            .Include(x => x.Claims)
            .SingleOrDefaultAsync(x => x.Id == model.RoleId);


        if (role == null || string.IsNullOrEmpty(role.Name))
            return false;

        var selectedPermissions = model.Permissions;

        var roleClaims = role.Claims
            .Where(x => x.ClaimType == ConstantPolicies.DynamicPermission)
            .Select(x => x.ClaimValue)
            .ToList();


        // add new permissions 
        var newPermissions = selectedPermissions.Except(roleClaims).ToList();
        foreach (var permission in newPermissions)
        {
            role.Claims.Add(new RoleClaim
            {
                ClaimType = ConstantPolicies.DynamicPermission,
                ClaimValue = permission,
                CreatedClaim = DateTime.UtcNow,
                RoleId = role.Id
            });
        }

        // remove deleted permissions
        var removedPermissions = roleClaims.Except(selectedPermissions).ToList();
        foreach (var permission in removedPermissions)
        {
            var roleClaim = role.Claims
                .SingleOrDefault(x =>
                    x.ClaimType == ConstantPolicies.DynamicPermission &&
                    x.ClaimValue == permission);

            if (roleClaim != null)
            {
                _db.RemoveRange(roleClaim);
            }
        }

        await _db.SaveChangesAsync();

        var result = await _roleManger.UpdateAsync(role);

        if (result.Succeeded)
        {
            var affectedUserIds = await _db.Set<UserEnterpriseMembership>()
                .Where(m => m.RoleId == role.Id && m.RevokedAt == null)
                .Select(m => m.UserId)
                .ToListAsync();

            foreach (var userId in affectedUserIds)
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user is not null)
                    await _userManager.UpdateSecurityStampAsync(user);
            }

            // Commit the SecurityStamp bumps from the loop above. AppUserStore.AutoSaveChanges
            // is false (Phase A), so UpdateSecurityStampAsync only stages the new stamp on the
            // tracked user; without this flush the stamps never reach the database and every
            // affected user keeps cruising with their old JWT until natural TTL expiry.
            await _db.SaveChangesAsync();

            return true;
        }

        return false;
    }

    public async Task<Option<Role>> GetRoleByIdAsync(Guid roleId)
    {
        return await _roleManger.FindByIdAsync(roleId.ToString());
    }

    public async Task<Option<Role>> GetRoleByIdIgnoringTenantAsync(Guid roleId)
    {
        // Bypasses the tenant query filter on Role so anonymous / cross-tenant invitation
        // flows can resolve a foreign-tenant role by its secret token. See interface docs.
        var role = await _db.Set<Role>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == roleId);
        return role is null ? Option<Role>.None : role;
    }

    public async Task<Option<Role>> GetRoleByNameAsync(string name)
    {
        return await _roleManger.FindByNameAsync(name);
    }

    public async Task<IReadOnlyList<string>> GetDynamicPermissionClaimsByRoleIdAsync(Guid roleId)
    {
        var claims = await _roleManger.Roles
            .Where(r => r.Id == roleId)
            .SelectMany(r => r.Claims)
            .Where(c => c.ClaimType == ConstantPolicies.DynamicPermission && c.ClaimValue != null)
            .Select(c => c.ClaimValue!)
            .ToListAsync();
        return claims;
    }

    public async Task<IReadOnlyList<string>> GetDynamicPermissionClaimsByRoleIdIgnoringTenantAsync(
        Guid roleId)
    {
        // IgnoreQueryFilters: SwitchEnterprise builds the next-session profile while the
        // request still carries the OLD JWT context, so the Role/RoleClaim tenant filter
        // would otherwise hide the target enterprise's role and return an empty permissions
        // list — making the SPA reload navigation with no entitlements until the user
        // switches a second time under the new bearer.
        var claims = await _db.Set<RoleClaim>()
            .IgnoreQueryFilters()
            .Where(c => c.RoleId == roleId
                        && c.ClaimType == ConstantPolicies.DynamicPermission
                        && c.ClaimValue != null)
            .Select(c => c.ClaimValue!)
            .ToListAsync();
        return claims;
    }

    public async Task<IReadOnlyList<System.Security.Claims.Claim>>
        GetAllClaimsByRoleIdIgnoringTenantAsync(Guid roleId)
    {
        // Same cross-tenant story as GetDynamicPermissionClaimsByRoleIdIgnoringTenantAsync,
        // but projects every claim (not just DynamicPermission) so JwtService can stamp the
        // freshly issued JWT with the role's full claim set. Without this the new bearer
        // ends up missing the permission claims under SwitchEnterprise's old-JWT context
        // and every protected request 403s ("Authorization Error") until a second switch.
        var rows = await _db.Set<RoleClaim>()
            .IgnoreQueryFilters()
            .Where(c => c.RoleId == roleId && c.ClaimType != null && c.ClaimValue != null)
            .Select(c => new { c.ClaimType, c.ClaimValue })
            .ToListAsync();
        return rows
            .Select(r => new System.Security.Claims.Claim(r.ClaimType!, r.ClaimValue!))
            .ToList();
    }
}