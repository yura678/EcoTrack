namespace Application.Common.Interfaces.Identity;

public interface ICurrentUserService
{
    Guid? GetCurrentEnterpriseId();
    Guid? GetCurrentUserId();
    bool IsSuperAdmin();


}