using Domain.Entities.User;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Identity.validator;

public class AppUserValidator(IdentityErrorDescriber errors) : UserValidator<User>(errors)
{
    public override async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user)
    {
        var result = await base.ValidateAsync(manager, user);
        
        return result;
    }
}