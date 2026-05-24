using Domain.Entities.User;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Infrastructure.Identity.Store;

public class AppUserStore : UserStore<User, Role, ApplicationDbContext, Guid, UserClaim, UserRole, UserLogin, UserToken,
    RoleClaim>
{
    public AppUserStore(ApplicationDbContext context, IdentityErrorDescriber describer = null) : base(context,
        describer)
    {
        // Identity mutations only stage changes in the DbContext; the caller (command handler)
        // is responsible for SaveChanges. Keeps every persistence boundary visible at the
        // handler level and lets us commit Identity changes atomically with business state.
        AutoSaveChanges = false;
    }
}