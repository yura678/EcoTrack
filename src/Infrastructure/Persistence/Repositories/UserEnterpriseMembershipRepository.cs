using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
using Application.Features.Users.Queries.GetUsers;
using Domain.Entities.User;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

internal class UserEnterpriseMembershipRepository(ApplicationDbContext context)
    : BaseAsyncRepository<UserEnterpriseMembership>(context),
        IUserEnterpriseMembershipRepository,
        IUserEnterpriseMembershipQueries
{
    public async Task<Option<UserEnterpriseMembership>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await Table.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity ?? Option<UserEnterpriseMembership>.None;
    }

    public async Task<Option<UserEnterpriseMembership>> GetByUserAndEnterpriseAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken)
    {
        var entity = await Table
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.EnterpriseId == enterpriseId,
                cancellationToken);
        return entity ?? Option<UserEnterpriseMembership>.None;
    }

    public async Task<IReadOnlyList<UserEnterpriseMembership>> GetActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        return await Table
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserEnterpriseMembership>> GetActiveByUserIdsForEnterpriseAsync(
        Guid enterpriseId, CancellationToken cancellationToken)
    {
        return await Table
            .Where(x => x.EnterpriseId == enterpriseId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserEnterpriseMembership>> GetByUserIdWithRoleAndEnterpriseAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: cross-tenant by design — return every active membership for
        // the user, including those whose Role lives in a foreign enterprise. The default
        // Role filter (EnterpriseId == TenantFilterId) would otherwise drop foreign-tenant
        // rows via the INNER JOIN on the required Role navigation.
        // The explicit `UserId == userId` predicate is the user-scope security boundary, and
        // we manually re-apply the soft-delete check on Enterprise that the filter would
        // otherwise enforce.
        return await TableNoTracking
            .IgnoreQueryFilters()
            .Where(x => x.UserId == userId
                        && x.RevokedAt == null
                        && x.Enterprise!.DeletedAt == null)
            .Include(x => x.Enterprise)
            .Include(x => x.Role)
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<UserEnterpriseMembership>> GetActiveByUserAndEnterpriseWithRoleAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken)
    {
        // IgnoreQueryFilters: same reason as GetByUserIdWithRoleAndEnterpriseAsync above —
        // used by UserProfileBuilder and SwitchEnterpriseCommand to resolve role/permissions
        // for the target enterprise, which may differ from the JWT's current tenant.
        // The (UserId, EnterpriseId) predicate is the user-scope security boundary.
        var entity = await TableNoTracking
            .IgnoreQueryFilters()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(
                x => x.UserId == userId && x.EnterpriseId == enterpriseId && x.RevokedAt == null,
                cancellationToken);
        return entity ?? Option<UserEnterpriseMembership>.None;
    }

    public async Task<IReadOnlyList<GetUsersQueryResponse>> GetAdminListAsync(
        Guid? enterpriseId, CancellationToken cancellationToken)
    {
        // LastLogin is a correlated subquery over LoginAttempt — avoids materialising the whole
        // history. EnterpriseId == null returns one row per (user, enterprise) for superAdmin.
        // Includes revoked memberships so admins can find and restore them; the projected
        // `IsActive` flag lets the UI distinguish active vs revoked rows.
        var query = TableNoTracking.AsQueryable();

        if (enterpriseId.HasValue)
        {
            query = query.Where(m => m.EnterpriseId == enterpriseId.Value);
        }

        var nowUtc = DateTimeOffset.UtcNow;
        var result = await query
            .Select(m => new GetUsersQueryResponse(
                m.UserId,
                m.User!.Email!,
                m.User!.UserName!,
                m.User!.Name,
                m.User!.FamilyName,
                m.RoleId,
                m.Role!.Name,
                m.RevokedAt == null,
                m.User!.LockoutEnd != null && m.User!.LockoutEnd > nowUtc,
                m.User!.EmailConfirmed,
                DbContext.Set<LoginAttempt>()
                    .Where(l => l.UserId == m.UserId && l.Outcome == LoginOutcome.Success)
                    .Max(l => (DateTime?)l.OccurredAt),
                m.EnterpriseId,
                m.Enterprise!.Name))
            .ToListAsync(cancellationToken);

        return result;
    }
}
