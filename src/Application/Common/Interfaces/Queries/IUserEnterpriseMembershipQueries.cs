using Application.Features.Users.Queries.GetUsers;
using Domain.Entities.User;
using LanguageExt;

namespace Application.Common.Interfaces.Queries;

public interface IUserEnterpriseMembershipQueries
{
    Task<IReadOnlyList<UserEnterpriseMembership>> GetByUserIdWithRoleAndEnterpriseAsync(
        Guid userId, CancellationToken cancellationToken);

    Task<Option<UserEnterpriseMembership>> GetActiveByUserAndEnterpriseWithRoleAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken);

    /// <summary>
    /// Read-model projection for the admin Users page. Joins membership + role + user +
    /// last successful login. If <paramref name="enterpriseId"/> is null, returns all active
    /// memberships across tenants (superAdmin view); otherwise scoped to that enterprise.
    /// </summary>
    Task<IReadOnlyList<GetUsersQueryResponse>> GetAdminListAsync(
        Guid? enterpriseId, CancellationToken cancellationToken);
}
