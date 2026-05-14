namespace Domain.Common;

public interface ITenantOwned
{
    Guid EnterpriseId { get; }

    void AssignTenant(Guid enterpriseId);
}
