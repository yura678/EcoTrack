using Domain.Entities.User;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.validator;

public class AppRoleValidator(IdentityErrorDescriber errors) : RoleValidator<Role>(errors)
{
    public override async Task<IdentityResult> ValidateAsync(RoleManager<Role> manager, Role role)
    {
        var result = await base.ValidateAsync(manager, role);

        return result;
    }
}