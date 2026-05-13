using Domain.Entities.User;

namespace Application.Common.Interfaces.Queries;

public interface IUserEnterpriseMembershipQueries
{
    Task<IReadOnlyList<UserEnterpriseMembership>> GetByUserIdWithRoleAndEnterpriseAsync(
        Guid userId, CancellationToken cancellationToken);
}
