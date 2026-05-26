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
    Task<Option<Role>> GetRoleByNameAsync(string name);

    /// <summary>
    /// Returns the dynamic-permission claim values assigned to the given role. Used by the
    /// profile endpoint to deliver the SPA an effective permissions list, since the JWT is
    /// JWE-encrypted and the client cannot read its own claims.
    /// </summary>
    Task<IReadOnlyList<string>> GetDynamicPermissionClaimsByRoleIdAsync(Guid roleId);
}