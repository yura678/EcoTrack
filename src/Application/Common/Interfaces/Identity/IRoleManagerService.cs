using Application.Features.Role.Exceptions;
using Application.Models.Identity;
using Domain.Entities.User;
using LanguageExt;
using Microsoft.AspNetCore.Identity;

namespace Application.Common.Interfaces.Identity;

public interface IRoleManagerService
{
    Task<List<GetRolesDto>> GetRolesAsync();
    Task<List<GetRolesDto>> GetEnterpriseRolesAsync(Guid enterpriseId);
    Task<(IdentityResult Result, Guid? CreatedRoleId)> CreateRoleAsync(CreateRoleDto model);
    Task SeedAllDynamicPermissionsAsync(Guid roleId);

    /// <summary>
    /// Deletes a role after verifying nothing still references it. Returns
    /// <see cref="RoleInUseException"/> when any membership (active or revoked) or pending
    /// invitation blocks the delete, or <see cref="RoleNotFoundException"/> when the role
    /// doesn't exist. Successful path bumps the SecurityStamp of every assigned user so
    /// their JWTs become invalid on the next request.
    /// </summary>
    Task<Either<RoleException, bool>> DeleteRoleAsync(Guid roleId);
    Task<List<ActionDescriptionDto>> GetPermissionActionsAsync();
    Task<Option<RolePermissionDto>> GetRolePermissionsAsync(Guid roleId);
    Task<bool> ChangeRolePermissionsAsync(EditRolePermissionsDto model);
    Task<Option<Role>> GetRoleByIdAsync(Guid roleId);

    /// <summary>
    /// Same as <see cref="GetRoleByIdAsync"/> but bypasses the tenant query filter on
    /// <see cref="Role"/>. Reserved for cross-tenant flows that legitimately operate on a
    /// foreign tenant's role with only a secret credential — the invitation preview /
    /// register-by-invitation / accept-invitation paths, where the token itself is the
    /// authorisation. Do NOT use from any tenant-context handler.
    /// </summary>
    Task<Option<Role>> GetRoleByIdIgnoringTenantAsync(Guid roleId);

    Task<Option<Role>> GetRoleByNameAsync(string name);

    /// <summary>
    /// Returns the dynamic-permission claim values assigned to the given role. Used by the
    /// profile endpoint to deliver the SPA an effective permissions list, since the JWT is
    /// JWE-encrypted and the client cannot read its own claims.
    /// </summary>
    Task<IReadOnlyList<string>> GetDynamicPermissionClaimsByRoleIdAsync(Guid roleId);

    /// <summary>
    /// Same as <see cref="GetDynamicPermissionClaimsByRoleIdAsync"/> but bypasses the tenant
    /// query filter on <see cref="Role"/> / <see cref="Domain.Entities.User.RoleClaim"/>.
    /// Reserved for cross-tenant flows where the caller's JWT tenant is intentionally
    /// different from the role's tenant — e.g. <c>SwitchEnterprise</c> builds the next
    /// session profile under the OLD JWT context, before the new token is in flight. Do NOT
    /// use from tenant-scoped admin paths (Role management screens etc.).
    /// </summary>
    Task<IReadOnlyList<string>> GetDynamicPermissionClaimsByRoleIdIgnoringTenantAsync(Guid roleId);

    /// <summary>
    /// Returns every <see cref="System.Security.Claims.Claim"/> attached to the role,
    /// bypassing the tenant query filter on <see cref="Domain.Entities.User.RoleClaim"/>.
    /// Used by <c>JwtService</c> when re-issuing a token for a target enterprise different
    /// from the JWT context currently in scope (SwitchEnterprise, refresh); the standard
    /// <c>RoleManager.GetClaimsAsync</c> would otherwise see zero rows and emit a JWT
    /// without the role's DynamicPermission claims, causing backend 403s on the freshly
    /// issued token until a second token issuance.
    /// </summary>
    Task<IReadOnlyList<System.Security.Claims.Claim>> GetAllClaimsByRoleIdIgnoringTenantAsync(
        Guid roleId);
}