using Domain.Entities.User;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Infrastructure.Identity.Store;

public class RoleStore : RoleStore<Role, ApplicationDbContext, Guid, UserRole, RoleClaim>
{
    public RoleStore(ApplicationDbContext context, IdentityErrorDescriber describer = null) : base(context, describer)
    {
        // Same contract as AppUserStore — RoleManager mutations track only; callers must
        // SaveChanges. RoleManagerService should follow this pattern.
        AutoSaveChanges = false;
    }
}