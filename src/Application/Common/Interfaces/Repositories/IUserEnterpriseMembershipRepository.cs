using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Repositories;

public interface IUserEnterpriseMembershipRepository
{
    Task<Option<UserEnterpriseMembership>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Option<UserEnterpriseMembership>> GetByUserAndEnterpriseAsync(Guid userId,
        Guid enterpriseId, CancellationToken cancellationToken);

    Task<IReadOnlyList<UserEnterpriseMembership>> GetActiveByUserIdAsync(Guid userId,
        CancellationToken cancellationToken);

    Task<UserEnterpriseMembership> AddAsync(UserEnterpriseMembership entity,
        CancellationToken cancellationToken);

    UserEnterpriseMembership Update(UserEnterpriseMembership entity);
}
