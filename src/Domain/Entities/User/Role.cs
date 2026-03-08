using Domain.Common;
using Domain.Entities.Enterprises;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities.User;

public class Role : IdentityRole<Guid>, IEntity
{
    public Role()
    {
        CreatedDate = DateTime.UtcNow;
    }

    public string DisplayName { get; set; }
    public DateTime CreatedDate { get; set; }
    
    public Guid EnterpriseId { get; set; }
    public Enterprise? Enterprise { get; set; }
    public ICollection<RoleClaim> Claims { get; set; }
    public ICollection<UserRole> Users { get; set; }
}