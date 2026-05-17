using System.Security.Claims;
using Domain.Entities.User;
using Infrastructure.Identity.Manager;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Infrastructure.Identity;

public class AppUserClaimsPrincipleFactory : UserClaimsPrincipalFactory<User, Role>
{
    public AppUserClaimsPrincipleFactory(
        AppUserManager userManager,
        AppRoleManager roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        return base.GenerateClaimsAsync(user);
    }
}