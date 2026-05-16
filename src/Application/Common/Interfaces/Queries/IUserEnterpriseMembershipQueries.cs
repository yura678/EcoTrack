using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Queries;

public interface IUserEnterpriseMembershipQueries
{
    Task<IReadOnlyList<UserEnterpriseMembership>> GetByUserIdWithRoleAndEnterpriseAsync(
        Guid userId, CancellationToken cancellationToken);

    Task<Option<UserEnterpriseMembership>> GetActiveByUserAndEnterpriseWithRoleAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken);
}
