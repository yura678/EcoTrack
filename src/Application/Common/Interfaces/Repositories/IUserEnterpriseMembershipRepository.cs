using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories;

public interface IUserEnterpriseMembershipRepository
{
    Task<Option<UserEnterpriseMembership>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<UserEnterpriseMembership>> GetByUserAndEnterpriseAsync(Guid userId,
        Guid enterpriseId, CancellationToken cancellationToken);

    Task<Option<UserEnterpriseMembership>> GetActiveByUserAndEnterpriseWithRoleAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserEnterpriseMembership>> GetActiveByUserIdAsync(Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Active memberships for an enterprise — used by SuperAdmin approval flow to email
    /// every admin attached to the tenant once a decision is made.
    /// </summary>
    Task<IReadOnlyList<UserEnterpriseMembership>> GetActiveByUserIdsForEnterpriseAsync(
        Guid enterpriseId, CancellationToken cancellationToken);

    Task<UserEnterpriseMembership> AddAsync(UserEnterpriseMembership entity,
        CancellationToken cancellationToken);

    UserEnterpriseMembership Update(UserEnterpriseMembership entity);
}
