using Domain.Common;

namespace Domain.Entities.User;

public class UserRefreshToken : BaseEntity<Guid>
{
    public UserRefreshToken()
    {
        CreatedAt = DateTime.UtcNow;
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsValid { get; set; }
    public Guid? EnterpriseId { get; set; }
}
