using Domain.Entities.User;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity.Manager;

public class AppRoleManager:RoleManager<Role>
{
    private readonly IHttpContextAccessor _contextAccessor;
    
    public AppRoleManager(
        IRoleStore<Role> store,
        IEnumerable<IRoleValidator<Role>> roleValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors, 
        IHttpContextAccessor contextAccessor,
        ILogger<RoleManager<Role>> logger) : base(store, roleValidators, keyNormalizer, errors, logger)
    {
        _contextAccessor = contextAccessor;
    }
    
    protected override CancellationToken CancellationToken => 
        _contextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
}