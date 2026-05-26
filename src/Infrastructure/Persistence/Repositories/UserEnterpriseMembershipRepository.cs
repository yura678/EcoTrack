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
        return await TableNoTracking
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .Include(x => x.Enterprise)
            .Include(x => x.Role)
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<UserEnterpriseMembership>> GetActiveByUserAndEnterpriseWithRoleAsync(
        Guid userId, Guid enterpriseId, CancellationToken cancellationToken)
    {
        var entity = await TableNoTracking
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
