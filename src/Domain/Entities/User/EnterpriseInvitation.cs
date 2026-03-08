using Domain.Common;
using Domain.Entities.Enterprises;

namespace Domain.Entities.User;

public class EnterpriseInvitation: BaseEntity
{
    public Guid EnterpriseId { get; private set; }
    public Guid RoleId { get; private set; }
    public string Email { get; private set; }
    public string Token { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }

    public Enterprise? Enterprise { get; private set; }

    private EnterpriseInvitation(Guid enterpriseId, string email, Guid roleId)
    {
        Id = Guid.NewGuid();
        EnterpriseId = enterpriseId;
        Email = email;
        Token = Guid.NewGuid().ToString("N");
        IsUsed = false;
        RoleId = roleId;
    }

    public static EnterpriseInvitation Create(Guid enterpriseId, string phoneNumber, Guid roleId, int expireDays = 7)
    {
        return new(enterpriseId, phoneNumber, roleId)
        {
            ExpiresAt = DateTime.UtcNow.AddDays(expireDays)

        };
    }

    public void MarkAsUsed()
    {
        IsUsed = true;
    }
}