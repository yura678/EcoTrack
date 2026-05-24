using Application.Common.Interfaces.Queries;
using Application.Common.Interfaces.Repositories;
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
}
