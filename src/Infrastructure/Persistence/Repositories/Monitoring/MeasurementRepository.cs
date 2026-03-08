using Application.Common.Interfaces.Identity;
using Application.Common.Interfaces.Queries.Monitoring;
using Application.Common.Interfaces.Repositories.Monitoring;
using Application.Common.Models;
using Domain.Entities.Monitoring;
using Infrastructure.Persistence.Repositories.Common;
using LanguageExt;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories.Monitoring;

internal class MeasurementRepository(ApplicationDbContext context, ICurrentUserService currentUserService)
    : BaseAsyncRepository<Measurement>(context), IMeasurementRepository, IMeasurementQueries
{
    public async Task<Option<Measurement>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Id == id &&
                                      (isSuperAdmin || x.EmissionSource!.Installation!.Site!.EnterpriseId ==
                                          currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Measurement>.None;
    }

    public async Task<Option<Measurement>> GetByTimeStamp(DateTime timestamp, Guid pollutantId,
        Guid emissionSourceId,
        CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var entity = await base.TableNoTracking
            .FirstOrDefaultAsync(x => x.Timestamp == timestamp
                                      && x.PollutantId == pollutantId
                                      && x.EmissionSourceId == emissionSourceId
                                      && (isSuperAdmin || x.EmissionSource!.Installation!.Site!.EnterpriseId ==
                                          currentEnterpriseId),
                cancellationToken);

        return entity ?? Option<Measurement>.None;
    }

    public async Task<PageResult<Measurement>> GetPagedAsync(
        Guid installationId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken)
    {
        var currentEnterpriseId = currentUserService.GetCurrentEnterpriseId();
        bool isSuperAdmin = currentUserService.IsSuperAdmin();

        var query = base.TableNoTracking
            .Where(x => x.EmissionSource!.InstallationId == installationId)
            .Where(x => isSuperAdmin || x.EmissionSource!.Installation!.Site!.EnterpriseId == currentEnterpriseId);

        if (from.HasValue)
        {
            query = query.Where(x => x.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.Timestamp <= to.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PageResult<Measurement>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }
}