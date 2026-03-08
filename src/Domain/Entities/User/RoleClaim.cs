using Domain.Common;
using Domain.Entities.Enterprises;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.User;

public class RoleClaim : IdentityRoleClaim<Guid>, IEntity
{
    public RoleClaim()
    {
        CreatedClaim = DateTime.UtcNow;
    }

    public DateTime CreatedClaim { get; set; }
    public Role Role { get; set; }
    
}