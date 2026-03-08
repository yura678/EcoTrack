using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Enterprises;
using Application.Common.Interfaces.Repositories.Enterprises;
using Domain.Entities.Enterprises;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Enterprises;

internal class SiteRepository(ApplicationDbContext context, ICurrentUserService currentUserService)
    : BaseAsyncRepository<Site>(context),
        ISiteRepository, ISiteQueries
{
    public async Task<IReadOnlyList<Site>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await base.TableNoTracking
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Site>> GetByEnterpriseIdAsync(Guid enterpriseId,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();
        
        return await base.TableNoTracking
            .Where(x => x.EnterpriseId.Equals(enterpriseId) &&
                        (isSuperAdmin || x.EnterpriseId == currentEnterpriseId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<Site>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    //  якщо isSuperAdmin == true, перевірка EnterpriseId ігнорується на рівні БД
                    (isSuperAdmin || x.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Site>.None;
    }

    public async Task<bool> HasDependenciesAsync(Guid id, CancellationToken cancellationToken)
    {
        var hasDependencies = await DbContext.Set<Installation>().AnyAsync(x => x.SiteId.Equals(id), cancellationToken);

        return hasDependencies;
    }

    public async Task<Option<Site>> GetByIdWithInstallationsAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .Include(x => x.Installations)
            .FirstOrDefaultAsync(x =>
                    x.Id == id &&
                    (isSuperAdmin || x.EnterpriseId == currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Site>.None;
    }
}