using Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.User;

public class UserClaim : IdentityUserClaim<Guid>, IEntity
{
    public User? User { get; set; }
}