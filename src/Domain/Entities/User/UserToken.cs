using Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.User;

public class UserToken : IdentityUserToken<Guid>, IEntity
{
    public UserToken()
    {
        GeneratedTime = DateTime.UtcNow;
    }

    public User User { get; set; }
    public DateTime GeneratedTime { get; set; }
}