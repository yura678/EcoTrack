using Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.User;

public class UserLogin : IdentityUserLogin<Guid>, IEntity
{
    public UserLogin()
    {
        LoggedOn = DateTime.UtcNow;
    }

    public User? User { get; set; }
    public DateTime LoggedOn { get; set; }
}