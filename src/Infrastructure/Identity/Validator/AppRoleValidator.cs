using Domain.Entities.User;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Identity.validator;

/// <summary>
/// Role validator that understands EcoTrack's tenant-scoped role naming. The stock
/// <see cref="RoleValidator{TRole}"/> rejects any role whose NormalizedName already exists,
/// which is wrong for this codebase: every enterprise has its own "admin" role, scoped by
/// <see cref="Role.EnterpriseId"/>. We replace the duplicate-check with a composite lookup
/// against (NormalizedName, EnterpriseId), matching the unique DB index.
/// </summary>
public class AppRoleValidator(
    ApplicationDbContext dbContext,
    IdentityErrorDescriber errors)
    : RoleValidator<Role>(errors)
{
    private readonly IdentityErrorDescriber _errors = errors;

    public override async Task<IdentityResult> ValidateAsync(RoleManager<Role> manager, Role role)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(role);

        var validationErrors = new List<IdentityError>();

        var roleName = await manager.GetRoleNameAsync(role);
        if (string.IsNullOrWhiteSpace(roleName))
        {
            validationErrors.Add(_errors.InvalidRoleName(roleName));
        }
        else
        {
            var normalizedName = manager.NormalizeKey(roleName);

            // IgnoreQueryFilters so the global tenant filter doesn't hide rows in unrelated
            // enterprises — the unique index spans the whole table regardless of which tenant
            // is "active" in the current request scope.
            var duplicate = await dbContext.Set<Role>()
                .IgnoreQueryFilters()
                .Where(r => r.NormalizedName == normalizedName
                            && r.EnterpriseId == role.EnterpriseId
                            && r.Id != role.Id)
                .FirstOrDefaultAsync();

            if (duplicate is not null)
            {
                validationErrors.Add(_errors.DuplicateRoleName(roleName));
            }
        }

        return validationErrors.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed(validationErrors.ToArray());
    }
}
