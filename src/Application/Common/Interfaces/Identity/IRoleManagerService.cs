using Application.Models.Identity;
using Domain.Entities.User;
using LanguageExt;
using Microsoft.AspNetCore.Identity;

namespace Application.Common.Interfaces.Identity;

public interface IRoleManagerService
{
    Task<List<GetRolesDto>> GetRolesAsync();
    Task<List<GetRolesDto>> GetEnterpriseRolesAsync(Guid enterpriseId);
    Task<IdentityResult> CreateRoleAsync(CreateRoleDto model);
    Task<bool> DeleteRoleAsync(Guid roleId);
    Task<List<ActionDescriptionDto>> GetPermissionActionsAsync();
    Task<Option<RolePermissionDto>> GetRolePermissionsAsync(Guid roleId);
    Task<bool> ChangeRolePermissionsAsync(EditRolePermissionsDto model);
    Task<Option<Role>> GetRoleByIdAsync(Guid roleId);
    Task<Option<Role>> GetRoleByNameAsync(string name);
}